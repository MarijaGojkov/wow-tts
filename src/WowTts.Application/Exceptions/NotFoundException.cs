namespace WowTts.Application.Exceptions;

// Something the caller asked for doesn't exist, e.g. an unknown quest. The Api layer turns this into a 404 Not Found.
public sealed class NotFoundException(string message) : Exception(message);
