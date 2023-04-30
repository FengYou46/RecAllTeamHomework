namespace RecAll.Infrastructure.Api; 

public class ServiceResultViewModel {
    public ServiceResultStatus Status { get; set; }

    public IEnumerable<string> Messages { get; set; }

    public ExceptionViewModel Exception { get; set; }

    public static ServiceResultViewModel
        FromServiceResult(ServiceResult result) =>
        result == null
            ? throw new ArgumentNullException(nameof(result))
            : new ServiceResultViewModel {
                Status = result.Status,
                Messages = result.Messages,
                Exception = ExceptionViewModel.FromException(result.Exception)
            };

    public virtual ServiceResult ToServiceResult() =>
        new() {
            Status = Status,
            Messages = Messages,
            Exception = Exception?.ToDeserializedException()
        };
}

public class ServiceResultViewModel<TResult> : ServiceResultViewModel {
    public TResult Result { get; set; }

    public static ServiceResultViewModel<TResult>
        FromServiceResult(ServiceResult<TResult> result) =>
        result == null
            ? throw new ArgumentNullException(nameof(result))
            : new ServiceResultViewModel<TResult> {
                Status = result.Status,
                Messages = result.Messages,
                Exception = ExceptionViewModel.FromException(result.Exception),
                Result = result.Result
            };

    public override ServiceResult<TResult> ToServiceResult() =>
        new() {
            Status = Status,
            Messages = Messages,
            Exception = Exception?.ToDeserializedException(),
            Result = Result
        };
}

public class ExceptionViewModel {
    public string Message { get; set; }

    public string Source { get; set; }

    public string HelpLink { get; set; }

    public string StackTrace { get; set; }

    public static ExceptionViewModel FromException(Exception e) =>
        e == null
            ? null
            : new ExceptionViewModel {
                Message = e.Message,
                Source = e.Source,
                HelpLink = e.HelpLink,
                StackTrace = e.StackTrace
            };

    public DeserializedException ToDeserializedException() =>
        new(Message, Source, HelpLink, StackTrace);
}

public class DeserializedException : Exception {
    public override string Message { get; }

    public override string Source { get; set; }

    public override string HelpLink { get; set; }

    public override string StackTrace { get; }

    public DeserializedException(string message, string source, string helpLink,
        string stackTrace) {
        Message = message;
        Source = source;
        HelpLink = helpLink;
        StackTrace = stackTrace;
    }
}

public static class ServiceResultViewModelExtension {
    public static ServiceResultViewModel
        ToServiceResultViewModel(this ServiceResult serviceResult) =>
        ServiceResultViewModel.FromServiceResult(serviceResult);

    public static ServiceResultViewModel<TResult>
        ToServiceResultViewModel<TResult>(
            this ServiceResult<TResult> serviceResult) =>
        ServiceResultViewModel<TResult>.FromServiceResult(serviceResult);
}