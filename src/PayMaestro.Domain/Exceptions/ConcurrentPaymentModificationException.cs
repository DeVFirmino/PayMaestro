namespace PayMaestro.Domain.Exceptions;

/// <summary>
/// Another writer settled this payment first. The losing update is discarded rather than
/// overwriting an outcome it never saw.
/// </summary>
public class ConcurrentPaymentModificationException()
    : PayMaestroException("The payment was modified by another operation. Reload it and retry.");
