using Dobby.Core;

namespace Dobby.Contracts.PluginPages;

public sealed record PluginLocalizedText(
    string Key,
    IReadOnlyList<string>? Arguments = null);

public sealed record PluginLocalizationBundle(
    string Culture,
    IReadOnlyDictionary<string, string> Entries);

public sealed record PluginLocalizationCatalog(
    string DefaultCulture,
    IReadOnlyList<PluginLocalizationBundle> Bundles);

public abstract record PluginComponent(string ComponentId);

public sealed record PluginTextComponent(
    string ComponentId,
    PluginLocalizedText Text) : PluginComponent(ComponentId);

public sealed record PluginHeadingComponent(
    string ComponentId,
    PluginLocalizedText Text,
    int Level) : PluginComponent(ComponentId);

public sealed record PluginFieldGroupComponent(
    string ComponentId,
    PluginLocalizedText Title,
    IReadOnlyList<PluginInputComponent> Inputs) : PluginComponent(ComponentId);

public abstract record PluginInputComponent(
    string ComponentId,
    PluginLocalizedText Label,
    bool IsRequired) : PluginComponent(ComponentId);

public sealed record PluginTextInputComponent(
    string ComponentId,
    PluginLocalizedText Label,
    bool IsRequired,
    int MaximumLength,
    string? InitialValue = null) : PluginInputComponent(ComponentId, Label, IsRequired);

public sealed record PluginNumberInputComponent(
    string ComponentId,
    PluginLocalizedText Label,
    bool IsRequired,
    decimal? Minimum,
    decimal? Maximum,
    decimal? InitialValue = null) : PluginInputComponent(ComponentId, Label, IsRequired);

public sealed record PluginDateInputComponent(
    string ComponentId,
    PluginLocalizedText Label,
    bool IsRequired,
    DateOnly? Minimum,
    DateOnly? Maximum,
    DateOnly? InitialValue = null) : PluginInputComponent(ComponentId, Label, IsRequired);

public sealed record PluginChoiceOption(
    string Value,
    PluginLocalizedText Label);

public sealed record PluginChoiceInputComponent(
    string ComponentId,
    PluginLocalizedText Label,
    bool IsRequired,
    IReadOnlyList<PluginChoiceOption> Options,
    string? InitialValue = null) : PluginInputComponent(ComponentId, Label, IsRequired);

public sealed record PluginToggleInputComponent(
    string ComponentId,
    PluginLocalizedText Label,
    bool IsRequired,
    bool InitialValue = false) : PluginInputComponent(ComponentId, Label, IsRequired);

public sealed record PluginValidationMessage(
    string? ComponentId,
    PluginLocalizedText Message,
    PluginValidationSeverity Severity);

public enum PluginValidationSeverity
{
    Information,
    Warning,
    Error
}

public sealed record PluginValidationSummaryComponent(
    string ComponentId,
    IReadOnlyList<PluginValidationMessage> Messages) : PluginComponent(ComponentId);

public sealed record PluginTableColumn(
    string ColumnId,
    PluginLocalizedText Header);

public sealed record PluginTableRow(
    string RowId,
    IReadOnlyDictionary<string, string> Cells);

public sealed record PluginPageRequest(
    int PageIndex,
    int PageSize,
    string? SortColumnId = null,
    bool Descending = false);

public sealed record PluginTableComponent(
    string ComponentId,
    PluginLocalizedText Caption,
    IReadOnlyList<PluginTableColumn> Columns,
    IReadOnlyList<PluginTableRow> Rows,
    PluginPageRequest Page,
    int TotalRowCount,
    string? PageCommandId = null,
    PluginLocalizedText? PreviousPageLabel = null,
    PluginLocalizedText? NextPageLabel = null) : PluginComponent(ComponentId);

public sealed record PluginListItem(
    string ItemId,
    PluginLocalizedText Text);

public sealed record PluginListComponent(
    string ComponentId,
    PluginLocalizedText Caption,
    IReadOnlyList<PluginListItem> Items) : PluginComponent(ComponentId);

public sealed record PluginCommandDescriptor(
    string CommandId,
    PluginLocalizedText Label,
    string OperationKey,
    bool CanCancel = true,
    int TimeoutSeconds = 30,
    PluginLocalizedText? CancelLabel = null,
    bool IsVisible = true);

public sealed record PluginComponentPageModel(
    PluginPageCommandContext Context,
    PluginLocalizedText Title,
    PluginLocalizedText FailureText,
    PluginLocalizedText CancelledText,
    PluginLocalizationCatalog Localization,
    IReadOnlyList<PluginComponent> Components,
    IReadOnlyList<PluginCommandDescriptor> Commands) : IPluginPageModel;

public sealed record PluginCommandRequest(
    string PluginId,
    string RouteKey,
    string? UserId,
    string CorrelationId,
    string CommandId,
    string OperationKey,
    IReadOnlyDictionary<string, string> Values,
    PluginPageRequest? PageRequest = null);

public sealed record PluginCommandResult(
    PluginLocalizedText? Message = null,
    IReadOnlyList<PluginValidationMessage>? ValidationMessages = null,
    PluginComponentPageModel? UpdatedPage = null);

public interface IPluginPageCommandHandler
{
    ValueTask<Result<PluginCommandResult>> ExecuteCommandAsync(
        PluginCommandRequest request,
        CancellationToken cancellationToken);
}

public interface IPluginCommandDispatcher
{
    ValueTask<Result<PluginCommandResult>> DispatchAsync(
        PluginCommandRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
