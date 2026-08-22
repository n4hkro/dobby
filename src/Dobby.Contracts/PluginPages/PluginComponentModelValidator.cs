using System.Text.RegularExpressions;
using Dobby.Core;

namespace Dobby.Contracts.PluginPages;

public static partial class PluginComponentModelValidator
{
    public const int MaximumComponents = 128;
    public const int MaximumCommands = 32;
    public const int MaximumRowsPerPage = 100;

    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        "az-Latn-AZ",
        "en-US",
        "ru-RU"
    };

    public static Result Validate(PluginComponentPageModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            ValidateContext(model.Context);
            ValidateLocalization(model.Localization);
            ValidateLocalizedText(model.Title, model.Localization);
            ValidateLocalizedText(model.FailureText, model.Localization);
            ValidateLocalizedText(model.CancelledText, model.Localization);
            if (model.Components.Count is < 1 or > MaximumComponents ||
                model.Commands.Count > MaximumCommands)
            {
                return Result.Failure(ErrorCode.ValidationFailed);
            }

            var componentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var component in model.Components)
            {
                ValidateComponent(component, model.Localization, componentIds);
            }

            var commandIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var command in model.Commands)
            {
                ValidateToken(command.CommandId, 80);
                ValidateLocalizedText(command.Label, model.Localization);
                if (command.CancelLabel is not null)
                {
                    ValidateLocalizedText(command.CancelLabel, model.Localization);
                }
                if (!commandIds.Add(command.CommandId) ||
                    OperationKeyPattern().IsMatch(command.OperationKey) is false ||
                    command.TimeoutSeconds is < 1 or > 60 ||
                    (command.CanCancel && command.CancelLabel is null))
                {
                    return Result.Failure(ErrorCode.ValidationFailed);
                }
            }

            foreach (var table in model.Components.OfType<PluginTableComponent>())
            {
                if (table.PageCommandId is not null && !commandIds.Contains(table.PageCommandId))
                {
                    return Result.Failure(ErrorCode.ValidationFailed);
                }
            }

            return Result.Success();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure(ErrorCode.ValidationFailed);
        }
    }

    private static void ValidateContext(PluginPageCommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateToken(context.PluginId, 128);
        ValidateText(context.RouteKey, 256);
        ValidateText(context.CorrelationId, 64);
        if (context.UserId is not null)
        {
            ValidateText(context.UserId, 160);
        }
    }

    private static void ValidateLocalization(PluginLocalizationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!SupportedCultures.Contains(catalog.DefaultCulture) ||
            catalog.Bundles.Count is < 1 or > 3 ||
            catalog.Bundles.Select(bundle => bundle.Culture).Distinct(StringComparer.OrdinalIgnoreCase).Count() != catalog.Bundles.Count)
        {
            throw new InvalidOperationException("Localization catalog is malformed.");
        }

        var defaultBundle = catalog.Bundles.SingleOrDefault(bundle =>
            bundle.Culture.Equals(catalog.DefaultCulture, StringComparison.OrdinalIgnoreCase)) ??
            throw new InvalidOperationException("Default localization bundle is missing.");
        if (defaultBundle.Entries.Count is < 1 or > 512)
        {
            throw new InvalidOperationException("Localization bundle size is invalid.");
        }

        var baseline = defaultBundle.Entries.Keys.Order(StringComparer.Ordinal).ToArray();
        foreach (var bundle in catalog.Bundles)
        {
            if (!SupportedCultures.Contains(bundle.Culture) ||
                !bundle.Entries.Keys.Order(StringComparer.Ordinal).SequenceEqual(baseline, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Localization bundle key parity failed.");
            }

            foreach (var entry in bundle.Entries)
            {
                ValidateToken(entry.Key, 128);
                ValidateText(entry.Value, 1024);
            }
        }
    }

    private static void ValidateComponent(
        PluginComponent component,
        PluginLocalizationCatalog catalog,
        ISet<string> componentIds)
    {
        ArgumentNullException.ThrowIfNull(component);
        ValidateToken(component.ComponentId, 80);
        if (!componentIds.Add(component.ComponentId))
        {
            throw new InvalidOperationException("Component ids must be unique.");
        }

        switch (component)
        {
            case PluginTextComponent text:
                ValidateLocalizedText(text.Text, catalog);
                break;
            case PluginHeadingComponent heading:
                ValidateLocalizedText(heading.Text, catalog);
                if (heading.Level is < 1 or > 3) throw new InvalidOperationException();
                break;
            case PluginFieldGroupComponent group:
                ValidateLocalizedText(group.Title, catalog);
                if (group.Inputs.Count is < 1 or > 32) throw new InvalidOperationException();
                foreach (var input in group.Inputs) ValidateInput(input, catalog, componentIds);
                break;
            case PluginValidationSummaryComponent summary:
                if (summary.Messages.Count > 64) throw new InvalidOperationException();
                foreach (var message in summary.Messages) ValidateLocalizedText(message.Message, catalog);
                break;
            case PluginTableComponent table:
                ValidateLocalizedText(table.Caption, catalog);
                ValidatePage(table.Page);
                if (table.Columns.Count is < 1 or > 16 || table.Rows.Count > MaximumRowsPerPage || table.TotalRowCount < table.Rows.Count)
                    throw new InvalidOperationException();
                var columns = table.Columns.Select(column => column.ColumnId).ToHashSet(StringComparer.Ordinal);
                if (columns.Count != table.Columns.Count) throw new InvalidOperationException();
                foreach (var column in table.Columns)
                {
                    ValidateToken(column.ColumnId, 80);
                    ValidateLocalizedText(column.Header, catalog);
                }
                foreach (var row in table.Rows)
                {
                    ValidateToken(row.RowId, 128);
                    if (row.Cells.Count != columns.Count || row.Cells.Keys.Any(key => !columns.Contains(key))) throw new InvalidOperationException();
                    foreach (var value in row.Cells.Values) ValidateText(value, 512);
                }
                if (table.Rows.Select(row => row.RowId).Distinct(StringComparer.Ordinal).Count() != table.Rows.Count)
                    throw new InvalidOperationException();
                if (table.PageCommandId is null)
                {
                    if (table.PreviousPageLabel is not null || table.NextPageLabel is not null)
                        throw new InvalidOperationException();
                }
                else
                {
                    ValidateToken(table.PageCommandId, 80);
                    ValidateLocalizedText(table.PreviousPageLabel ?? throw new InvalidOperationException(), catalog);
                    ValidateLocalizedText(table.NextPageLabel ?? throw new InvalidOperationException(), catalog);
                }
                break;
            case PluginListComponent list:
                ValidateLocalizedText(list.Caption, catalog);
                if (list.Items.Count > MaximumRowsPerPage) throw new InvalidOperationException();
                foreach (var item in list.Items)
                {
                    ValidateToken(item.ItemId, 128);
                    ValidateLocalizedText(item.Text, catalog);
                }
                break;
            default:
                throw new InvalidOperationException("Unknown plugin component type.");
        }
    }

    private static void ValidateInput(
        PluginInputComponent input,
        PluginLocalizationCatalog catalog,
        ISet<string> componentIds)
    {
        ValidateToken(input.ComponentId, 80);
        ValidateLocalizedText(input.Label, catalog);
        if (!componentIds.Add(input.ComponentId)) throw new InvalidOperationException();
        switch (input)
        {
            case PluginTextInputComponent text:
                if (text.MaximumLength is < 1 or > 4096 ||
                    (text.InitialValue?.Length ?? 0) > text.MaximumLength)
                    throw new InvalidOperationException();
                break;
            case PluginNumberInputComponent number:
                if (number.Minimum > number.Maximum ||
                    (number.InitialValue is { } initialNumber &&
                     ((number.Minimum is { } minimum && initialNumber < minimum) ||
                      (number.Maximum is { } maximum && initialNumber > maximum))))
                    throw new InvalidOperationException();
                break;
            case PluginDateInputComponent date:
                if (date.Minimum > date.Maximum ||
                    (date.InitialValue is { } initialDate &&
                     ((date.Minimum is { } dateMinimum && initialDate < dateMinimum) ||
                      (date.Maximum is { } dateMaximum && initialDate > dateMaximum))))
                    throw new InvalidOperationException();
                break;
            case PluginChoiceInputComponent choice:
                if (choice.Options.Count is < 1 or > 128 ||
                    choice.Options.Select(option => option.Value).Distinct(StringComparer.Ordinal).Count() != choice.Options.Count ||
                    (choice.InitialValue is not null && choice.Options.All(option =>
                        !option.Value.Equals(choice.InitialValue, StringComparison.Ordinal))))
                    throw new InvalidOperationException();
                foreach (var option in choice.Options)
                {
                    ValidateToken(option.Value, 128);
                    ValidateLocalizedText(option.Label, catalog);
                }
                break;
        }
    }

    private static void ValidatePage(PluginPageRequest page)
    {
        if (page.PageIndex < 0 || page.PageSize is < 1 or > MaximumRowsPerPage)
            throw new InvalidOperationException();
        if (page.SortColumnId is not null) ValidateToken(page.SortColumnId, 80);
    }

    private static void ValidateLocalizedText(
        PluginLocalizedText text,
        PluginLocalizationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(text);
        ValidateToken(text.Key, 128);
        if (!catalog.Bundles[0].Entries.ContainsKey(text.Key) || (text.Arguments?.Count ?? 0) > 8)
            throw new InvalidOperationException();
        foreach (var argument in text.Arguments ?? []) ValidateText(argument, 128);
    }

    private static void ValidateToken(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || !TokenPattern().IsMatch(value))
            throw new ArgumentException("Token is malformed.");
    }

    private static void ValidateText(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength || value.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t'))
            throw new ArgumentException("Text is malformed.");
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._/-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*(?:\\.[A-Za-z0-9][A-Za-z0-9._-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex OperationKeyPattern();
}
