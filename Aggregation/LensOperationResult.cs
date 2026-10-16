namespace Aggregation;

public readonly struct LensOperationResult
{
	public bool Success { get; }

	public int ErrorCode { get; }

	public int Attempts { get; }

	public string ErrorMessage { get; }

	private LensOperationResult(bool success, int errorCode, int attempts, string errorMessage)
	{
		Success = success;
		ErrorCode = errorCode;
		Attempts = attempts;
		ErrorMessage = errorMessage;
	}

	public static LensOperationResult Ok(int attempts = 1)
	{
		return new LensOperationResult(success: true, 0, attempts, null);
	}

	public static LensOperationResult Fail(int errorCode, int attempts, string message = null)
	{
		return new LensOperationResult(success: false, errorCode, attempts, message);
	}

	public static LensOperationResult LensNotAvailable(string reason = null)
	{
		return new LensOperationResult(success: false, -1, 0, reason ?? "Lens not available");
	}

	public static LensOperationResult Disposed(int attempts = 0)
	{
		return new LensOperationResult(success: false, -99, attempts, "Controller disposed");
	}

	public override string ToString()
	{
		if (!Success)
		{
			return string.Format("FAIL (code: {0}, attempts: {1}, message: {2})", ErrorCode, Attempts, ErrorMessage ?? "none");
		}
		return $"OK (attempts: {Attempts})";
	}
}
