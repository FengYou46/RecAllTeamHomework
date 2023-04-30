using System.Collections.Immutable;

namespace RecAll.Infrastructure; 

public class ServiceResult {
    public ServiceResultStatus Status { get; init; }

    public IEnumerable<string> Messages { get; init; }

    public Exception Exception { get; init; }

    public ServiceResult<TResult> AsResult<TResult>() =>
        Status == ServiceResultStatus.Succeeded
            ? throw new InvalidOperationException(
                $"不能将{nameof(Status)}为{nameof(ServiceResultStatus.Succeeded)}的{nameof(ServiceResult)}转换为{nameof(ServiceResult<TResult>)}。")
            : new ServiceResult<TResult> {
                Status = Status, Messages = Messages, Exception = Exception
            };

    public static ServiceResult CreateSucceededResult() =>
        new() { Status = ServiceResultStatus.Succeeded };

    public static ServiceResult CreateFailedResult() =>
        new() { Status = ServiceResultStatus.Failed };

    public static ServiceResult CreateFailedResult(string message) =>
        CreateFailedResult(new[] {
            message ?? throw new ArgumentNullException(nameof(message))
        });

    public static ServiceResult
        CreateFailedResult(IEnumerable<string> messages) =>
        new() {
            Status = ServiceResultStatus.Failed,
            Messages = messages?.ToImmutableList() ??
                throw new ArgumentNullException(nameof(messages))
        };

    public static ServiceResult CreateExceptionResult(string message) =>
        CreateExceptionResult(new[] {
            message ?? throw new ArgumentNullException(nameof(message))
        });

    public static ServiceResult
        CreateExceptionResult(IEnumerable<string> messages) =>
        new() {
            Status = ServiceResultStatus.Exception,
            Messages = messages?.ToImmutableList() ??
                throw new ArgumentNullException(nameof(messages))
        };

    public static ServiceResult
        CreateExceptionResult(Exception e, string message) =>
        CreateExceptionResult(e ?? throw new ArgumentNullException(nameof(e)),
            new[] {
                message ?? throw new ArgumentNullException(nameof(message))
            });

    public static ServiceResult
        CreateExceptionResult(Exception e, IEnumerable<string> messages) =>
        new() {
            Status = ServiceResultStatus.Exception,
            Exception = e ?? throw new ArgumentNullException(nameof(e)),
            Messages = messages?.ToImmutableList() ??
                throw new ArgumentNullException(nameof(messages))
        };

    public static ServiceResult CreateInvalidParameterResult(string message) =>
        CreateInvalidParameterResult(new[] {
            message ?? throw new ArgumentNullException(nameof(message))
        });

    public static ServiceResult
        CreateInvalidParameterResult(IEnumerable<string> messages) =>
        new() {
            Status = ServiceResultStatus.InvalidParameter,
            Messages = messages?.ToImmutableList() ??
                throw new ArgumentNullException(nameof(messages))
        };
}

public class ServiceResult<TResult> : ServiceResult {
    public TResult Result { get; set; }

    public ServiceResult<TType>
        AsResult<TType>(Func<TResult, TType> converter) =>
        new() {
            Status = Status,
            Messages = Messages,
            Exception = Exception,
            Result = Status == ServiceResultStatus.Succeeded
                ? converter == null
                    ? throw new ArgumentNullException(nameof(converter))
                    : converter.Invoke(Result)
                : default
        };

    public static ServiceResult<TResult>
        CreateSucceededResult(TResult result) =>
        new() {
            Status = ServiceResultStatus.Succeeded,
            Result = result ?? throw new ArgumentNullException(nameof(result))
        };

    public new static ServiceResult<TResult> CreateFailedResult() =>
        new() { Status = ServiceResultStatus.Failed };

    public new static ServiceResult<TResult>
        CreateFailedResult(string message) =>
        CreateFailedResult(new[] {
            message ?? throw new ArgumentNullException(nameof(message))
        });

    public new static ServiceResult<TResult>
        CreateFailedResult(IEnumerable<string> messages) =>
        new() {
            Status = ServiceResultStatus.Failed,
            Messages = messages?.ToImmutableList() ??
                throw new ArgumentNullException(nameof(messages))
        };

    public static ServiceResult<TResult> CreateExceptionResult() =>
        new() { Status = ServiceResultStatus.Exception };

    public new static ServiceResult<TResult>
        CreateExceptionResult(string message) =>
        CreateExceptionResult(new[] {
            message ?? throw new ArgumentNullException(nameof(message))
        });

    public new static ServiceResult<TResult>
        CreateExceptionResult(IEnumerable<string> messages) =>
        new() {
            Status = ServiceResultStatus.Exception,
            Messages = messages?.ToImmutableList() ??
                throw new ArgumentNullException(nameof(messages))
        };

    public new static ServiceResult<TResult>
        CreateExceptionResult(Exception e, string message) =>
        CreateExceptionResult(e ?? throw new ArgumentNullException(nameof(e)),
            new[] {
                message ?? throw new ArgumentNullException(nameof(message))
            });

    public new static ServiceResult<TResult> CreateExceptionResult(Exception e,
        IEnumerable<string> messages) =>
        new() {
            Status = ServiceResultStatus.Exception,
            Exception = e ?? throw new ArgumentNullException(nameof(e)),
            Messages = messages?.ToImmutableList() ??
                throw new ArgumentNullException(nameof(messages))
        };

    public static ServiceResult<TResult>
        CreateInvalidParameterResult(string message) =>
        CreateInvalidParameterResult(new[] {
            message ?? throw new ArgumentNullException(nameof(message))
        });

    public static ServiceResult<TResult>
        CreateInvalidParameterResult(IEnumerable<string> messages) =>
        new() {
            Status = ServiceResultStatus.InvalidParameter,
            Messages = messages?.ToImmutableList() ??
                throw new ArgumentNullException(nameof(messages))
        };
}

public enum ServiceResultStatus {
    Succeeded = 1,
    Failed = 2,
    Exception = 4,
    InvalidParameter = 8
}