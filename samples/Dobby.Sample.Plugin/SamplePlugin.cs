using System.Globalization;
using Dobby.Contracts.BackgroundOperations;
using Dobby.Contracts.PluginFramework;
using Dobby.Contracts.PluginPages;
using Dobby.Core;
using Dobby.SDK.PluginFramework;

namespace Dobby.Sample.Plugin;

public sealed class SamplePluginModule : PluginBaseV2
{
    public override void RegisterServices(IPluginServiceCollection services)
    {
        services.AddExtensionPoint<IPluginPage, SampleHomePage>();
    }
}

public sealed class SampleHomePage(
    IBackgroundOperationReporter backgroundOperations,
    IPluginWorkSessionService workSessions) : IPluginPage, IPluginPageCommandHandler
{
    private const string GreetOperationKey = "com.dobby.sample.greet.execute";
    private const string SubmitCommandId = "sample.submit";
    private const string InvalidCommandId = "sample.invalid-input";
    private const string FaultCommandId = "sample.fault";
    private const string ProgressCommandId = "sample.progress";
    private const string WorkSessionCommandId = "sample.work-session";
    private const string PageCommandId = "sample.page";

    public PluginPageDescriptor Descriptor { get; } = new(
        "sample.greet.page",
        "greet",
        "sample.nav.greet");

    public ValueTask<Result<IPluginPageModel>> LoadAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        var operationId = Guid.CreateVersion7();
        var correlationId = GuidFrom(context.CorrelationId);
        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Plugin,
            context.PluginId,
            "sample.home.load",
            null,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Indeterminate(),
            CanCancel: false,
            correlationId,
            FailureReason: null));

        cancellationToken.ThrowIfCancellationRequested();
        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Plugin,
            context.PluginId,
            "sample.home.load",
            null,
            BackgroundOperationStatus.Completed,
            BackgroundOperationProgress.Determinate(100),
            CanCancel: false,
            correlationId,
            FailureReason: null));

        return ValueTask.FromResult(Result.Success<IPluginPageModel>(BuildPage(
            new PluginPageCommandContext(
                context.PluginId,
                context.RouteKey,
                context.UserId,
                context.CorrelationId),
            pageIndex: 0)));
    }

    public ValueTask<Result> ActivateAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result.Success());
    }

    public ValueTask<Result> DeactivateAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result.Success());
    }

    public ValueTask<Result> UnloadAsync(
        PluginPageContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result.Success());
    }

    public ValueTask<Result<PluginCommandResult>> ExecuteCommandAsync(
        PluginCommandRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return request.CommandId switch
        {
            SubmitCommandId => ExecuteSubmit(request),
            InvalidCommandId => ValueTask.FromResult(
                Result.Failure<PluginCommandResult>(ErrorCode.InvalidInput)),
            FaultCommandId => throw new InvalidOperationException("sample.fault.unhandled"),
            ProgressCommandId => ExecuteProgress(request, cancellationToken),
            WorkSessionCommandId => ExecuteWorkSession(request, cancellationToken),
            PageCommandId => ExecutePage(request),
            _ => ValueTask.FromResult(Result.Failure<PluginCommandResult>(ErrorCode.NotFound))
        };
    }

    private static ValueTask<Result<PluginCommandResult>> ExecuteSubmit(PluginCommandRequest request)
    {
        if (!request.Values.TryGetValue("customer-name", out var customerName) ||
            string.IsNullOrWhiteSpace(customerName))
        {
            return ValueTask.FromResult(Result.Success(new PluginCommandResult(
                ValidationMessages:
                [
                    new PluginValidationMessage(
                        "customer-name",
                        new PluginLocalizedText("validation.name.required"),
                        PluginValidationSeverity.Error)
                ])));
        }

        if (!request.Values.TryGetValue("quantity", out var quantityText) ||
            !decimal.TryParse(quantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) ||
            quantity is < 1 or > 100)
        {
            return ValueTask.FromResult(Result.Success(new PluginCommandResult(
                ValidationMessages:
                [
                    new PluginValidationMessage(
                        "quantity",
                        new PluginLocalizedText("validation.quantity.range"),
                        PluginValidationSeverity.Error)
                ])));
        }

        return ValueTask.FromResult(Result.Success(new PluginCommandResult(
            new PluginLocalizedText("command.submit.success", [customerName]))));
    }

    private async ValueTask<Result<PluginCommandResult>> ExecuteProgress(
        PluginCommandRequest request,
        CancellationToken cancellationToken)
    {
        var operationId = Guid.CreateVersion7();
        var correlationId = GuidFrom(request.CorrelationId);
        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Plugin,
            request.PluginId,
            "sample.greet.progress",
            null,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Determinate(0),
            CanCancel: true,
            correlationId,
            FailureReason: null));

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Plugin,
            request.PluginId,
            "sample.greet.progress",
            null,
            BackgroundOperationStatus.Running,
            BackgroundOperationProgress.Determinate(50),
            CanCancel: true,
            correlationId,
            FailureReason: null));

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        backgroundOperations.Progress.Report(new BackgroundOperationSnapshot(
            operationId,
            BackgroundOperationOwnerKind.Plugin,
            request.PluginId,
            "sample.greet.progress",
            null,
            BackgroundOperationStatus.Completed,
            BackgroundOperationProgress.Determinate(100),
            CanCancel: false,
            correlationId,
            FailureReason: null));
        return Result.Success(new PluginCommandResult(
            new PluginLocalizedText("command.progress.success")));
    }

    private ValueTask<Result<PluginCommandResult>> ExecuteWorkSession(
        PluginCommandRequest request,
        CancellationToken cancellationToken)
    {
        var session = workSessions.Open(request.PluginId, request.RouteKey, "sample.work-session");
        if (!session.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<PluginCommandResult>(
                session.ErrorCode ?? ErrorCode.PluginInternalError));
        }

        var progress = workSessions.UpdateProgress(session.Value.SessionId, 50);
        if (!progress.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<PluginCommandResult>(
                progress.ErrorCode ?? ErrorCode.PluginInternalError));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = workSessions.RecordResult(session.Value.SessionId, "sample.work-session.completed");
        if (!result.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<PluginCommandResult>(
                result.ErrorCode ?? ErrorCode.PluginInternalError));
        }

        var close = workSessions.Close(session.Value.SessionId);
        if (!close.IsSuccess)
        {
            return ValueTask.FromResult(Result.Failure<PluginCommandResult>(
                close.ErrorCode ?? ErrorCode.PluginInternalError));
        }

        return ValueTask.FromResult(Result.Success(new PluginCommandResult(
            new PluginLocalizedText("command.work-session.success"))));
    }

    private static ValueTask<Result<PluginCommandResult>> ExecutePage(PluginCommandRequest request)
    {
        if (request.PageRequest is not { PageIndex: >= 0 } pageRequest)
        {
            return ValueTask.FromResult(Result.Failure<PluginCommandResult>(ErrorCode.InvalidInput));
        }

        var context = new PluginPageCommandContext(
            request.PluginId,
            request.RouteKey,
            request.UserId,
            request.CorrelationId);
        return ValueTask.FromResult(Result.Success(new PluginCommandResult(
            UpdatedPage: BuildPage(context, pageRequest.PageIndex))));
    }

    private static PluginComponentPageModel BuildPage(PluginPageCommandContext context, int pageIndex)
    {
        var allRows = new[]
        {
            new PluginTableRow("row-1", Cells("REQ-001", "Ready")),
            new PluginTableRow("row-2", Cells("REQ-002", "Review")),
            new PluginTableRow("row-3", Cells("REQ-003", "Ready")),
            new PluginTableRow("row-4", Cells("REQ-004", "Blocked")),
            new PluginTableRow("row-5", Cells("REQ-005", "Review")),
            new PluginTableRow("row-6", Cells("REQ-006", "Ready"))
        };
        const int pageSize = 3;
        var boundedPageIndex = Math.Clamp(pageIndex, 0, (allRows.Length - 1) / pageSize);
        var rows = allRows.Skip(boundedPageIndex * pageSize).Take(pageSize).ToArray();

        return new PluginComponentPageModel(
            context,
            new PluginLocalizedText("page.title"),
            new PluginLocalizedText("status.failure"),
            new PluginLocalizedText("status.cancelled"),
            Localization,
            [
                new PluginTextComponent("intro", new PluginLocalizedText("page.intro")),
                new PluginFieldGroupComponent(
                    "request-form",
                    new PluginLocalizedText("form.title"),
                    [
                        new PluginTextInputComponent(
                            "customer-name",
                            new PluginLocalizedText("field.name"),
                            IsRequired: true,
                            MaximumLength: 128),
                        new PluginNumberInputComponent(
                            "quantity",
                            new PluginLocalizedText("field.quantity"),
                            IsRequired: true,
                            Minimum: 1,
                            Maximum: 100,
                            InitialValue: 1),
                        new PluginDateInputComponent(
                            "due-date",
                            new PluginLocalizedText("field.date"),
                            IsRequired: true,
                            Minimum: new DateOnly(2020, 1, 1),
                            Maximum: new DateOnly(2100, 12, 31),
                            InitialValue: new DateOnly(2026, 8, 22)),
                        new PluginChoiceInputComponent(
                            "service-level",
                            new PluginLocalizedText("field.service"),
                            IsRequired: true,
                            [
                                new PluginChoiceOption("standard", new PluginLocalizedText("choice.standard")),
                                new PluginChoiceOption("priority", new PluginLocalizedText("choice.priority"))
                            ],
                            InitialValue: "standard"),
                        new PluginToggleInputComponent(
                            "notify",
                            new PluginLocalizedText("field.notify"),
                            IsRequired: false,
                            InitialValue: true)
                    ]),
                new PluginValidationSummaryComponent("validation-summary", []),
                new PluginTableComponent(
                    "requests",
                    new PluginLocalizedText("table.caption"),
                    [
                        new PluginTableColumn("reference", new PluginLocalizedText("table.reference")),
                        new PluginTableColumn("status", new PluginLocalizedText("table.status"))
                    ],
                    rows,
                    new PluginPageRequest(boundedPageIndex, pageSize, "reference"),
                    allRows.Length,
                    PageCommandId,
                    new PluginLocalizedText("paging.previous"),
                    new PluginLocalizedText("paging.next")),
                new PluginListComponent(
                    "guidance",
                    new PluginLocalizedText("list.caption"),
                    [
                        new PluginListItem("guidance-1", new PluginLocalizedText("list.required")),
                        new PluginListItem("guidance-2", new PluginLocalizedText("list.review"))
                    ])
            ],
            [
                Command(SubmitCommandId, "command.submit", canCancel: true),
                Command(InvalidCommandId, "command.invalid"),
                Command(FaultCommandId, "command.fault"),
                Command(ProgressCommandId, "command.progress", canCancel: true),
                Command(WorkSessionCommandId, "command.work-session", canCancel: true),
                Command(PageCommandId, "command.page") with { IsVisible = false }
            ]);
    }

    private static PluginCommandDescriptor Command(string id, string label, bool canCancel = false) =>
        new(
            id,
            new PluginLocalizedText(label),
            GreetOperationKey,
            canCancel,
            TimeoutSeconds: 15,
            CancelLabel: canCancel ? new PluginLocalizedText("command.cancel") : null);

    private static IReadOnlyDictionary<string, string> Cells(string reference, string status) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["reference"] = reference,
            ["status"] = status
        };

    private static Guid GuidFrom(string value) =>
        Guid.TryParse(value, out var parsed) ? parsed : Guid.CreateVersion7();

    private static PluginLocalizationCatalog Localization { get; } = new(
        "en-US",
        [
            Bundle("az-Latn-AZ", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page.title"] = "Nümunə sorğu iş sahəsi",
                ["page.intro"] = "Sorğunu daxil edin, cədvəli səhifələyin və əmri təhlükəsiz host sərhədindən icra edin.",
                ["form.title"] = "Sorğu detalları",
                ["field.name"] = "Müştəri adı",
                ["field.quantity"] = "Miqdar",
                ["field.date"] = "Son tarix",
                ["field.service"] = "Xidmət səviyyəsi",
                ["field.notify"] = "Tamamlananda bildiriş göndər",
                ["choice.standard"] = "Standart",
                ["choice.priority"] = "Prioritet",
                ["table.caption"] = "Son sorğular",
                ["table.reference"] = "İstinad",
                ["table.status"] = "Vəziyyət",
                ["paging.previous"] = "Əvvəlki səhifə",
                ["paging.next"] = "Növbəti səhifə",
                ["list.caption"] = "Yoxlama siyahısı",
                ["list.required"] = "Bütün məcburi sahələri doldurun.",
                ["list.review"] = "Göndərməzdən əvvəl detalları yoxlayın.",
                ["command.submit"] = "Sorğunu göndər",
                ["command.invalid"] = "Yanlış daxiletməni sına",
                ["command.fault"] = "Xəta sərhədini sına",
                ["command.progress"] = "İrəliləyişi sına",
                ["command.work-session"] = "İş sessiyası aç",
                ["command.page"] = "Cədvəl səhifəsi",
                ["command.cancel"] = "Ləğv et",
                ["command.submit.success"] = "{0} üçün sorğu qəbul edildi.",
                ["command.progress.success"] = "İrəliləyiş əməliyyatı tamamlandı.",
                ["command.work-session.success"] = "İş sessiyası tamamlandı.",
                ["validation.name.required"] = "Müştəri adı məcburidir.",
                ["validation.quantity.range"] = "Miqdar 1 ilə 100 arasında olmalıdır.",
                ["status.failure"] = "Əmr təhlükəsiz şəkildə tamamlanmadı.",
                ["status.cancelled"] = "Əmr ləğv edildi."
            }),
            Bundle("en-US", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page.title"] = "Sample request workspace",
                ["page.intro"] = "Enter a request, page the table, and execute commands through the guarded host boundary.",
                ["form.title"] = "Request details",
                ["field.name"] = "Customer name",
                ["field.quantity"] = "Quantity",
                ["field.date"] = "Due date",
                ["field.service"] = "Service level",
                ["field.notify"] = "Notify when complete",
                ["choice.standard"] = "Standard",
                ["choice.priority"] = "Priority",
                ["table.caption"] = "Recent requests",
                ["table.reference"] = "Reference",
                ["table.status"] = "Status",
                ["paging.previous"] = "Previous page",
                ["paging.next"] = "Next page",
                ["list.caption"] = "Checklist",
                ["list.required"] = "Complete every required field.",
                ["list.review"] = "Review the details before submitting.",
                ["command.submit"] = "Submit request",
                ["command.invalid"] = "Test invalid input",
                ["command.fault"] = "Test fault boundary",
                ["command.progress"] = "Test progress",
                ["command.work-session"] = "Open work session",
                ["command.page"] = "Table page",
                ["command.cancel"] = "Cancel",
                ["command.submit.success"] = "The request for {0} was accepted.",
                ["command.progress.success"] = "The progress operation completed.",
                ["command.work-session.success"] = "The work session was completed.",
                ["validation.name.required"] = "Customer name is required.",
                ["validation.quantity.range"] = "Quantity must be between 1 and 100.",
                ["status.failure"] = "The command could not be completed safely.",
                ["status.cancelled"] = "The command was cancelled."
            }),
            Bundle("ru-RU", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["page.title"] = "Рабочая область примера запроса",
                ["page.intro"] = "Введите запрос, перелистывайте таблицу и выполняйте команды через защищённую границу хоста.",
                ["form.title"] = "Детали запроса",
                ["field.name"] = "Имя клиента",
                ["field.quantity"] = "Количество",
                ["field.date"] = "Срок выполнения",
                ["field.service"] = "Уровень обслуживания",
                ["field.notify"] = "Уведомить после завершения",
                ["choice.standard"] = "Стандартный",
                ["choice.priority"] = "Приоритетный",
                ["table.caption"] = "Последние запросы",
                ["table.reference"] = "Ссылка",
                ["table.status"] = "Статус",
                ["paging.previous"] = "Предыдущая страница",
                ["paging.next"] = "Следующая страница",
                ["list.caption"] = "Контрольный список",
                ["list.required"] = "Заполните все обязательные поля.",
                ["list.review"] = "Проверьте данные перед отправкой.",
                ["command.submit"] = "Отправить запрос",
                ["command.invalid"] = "Проверить неверный ввод",
                ["command.fault"] = "Проверить границу ошибок",
                ["command.progress"] = "Проверить прогресс",
                ["command.work-session"] = "Открыть рабочую сессию",
                ["command.page"] = "Страница таблицы",
                ["command.cancel"] = "Отмена",
                ["command.submit.success"] = "Запрос для {0} принят.",
                ["command.progress.success"] = "Операция прогресса завершена.",
                ["command.work-session.success"] = "Рабочая сессия завершена.",
                ["validation.name.required"] = "Имя клиента обязательно.",
                ["validation.quantity.range"] = "Количество должно быть от 1 до 100.",
                ["status.failure"] = "Команду не удалось безопасно завершить.",
                ["status.cancelled"] = "Команда отменена."
            })
        ]);

    private static PluginLocalizationBundle Bundle(
        string culture,
        IReadOnlyDictionary<string, string> entries) => new(culture, entries);
}
