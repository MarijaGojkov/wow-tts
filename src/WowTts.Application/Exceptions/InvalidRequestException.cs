namespace WowTts.Application.Exceptions;

// The caller sent something we can't work with. The Api layer turns this into a 400 Bad Request.
public sealed class InvalidRequestException(string message) : Exception(message);
