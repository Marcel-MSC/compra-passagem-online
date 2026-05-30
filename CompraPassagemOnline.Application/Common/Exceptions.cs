namespace CompraPassagemOnline.Application.Common;

public sealed class SeatConflictException : Exception
{
    public SeatConflictException(string message) : base(message) { }
}

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public sealed class InvalidOperationDomainException : Exception
{
    public InvalidOperationDomainException(string message) : base(message) { }
}
