namespace WowTts.Application.Exceptions;

// The speech service failed, e.g. a wrong key or region. The Api layer turns this into a 502 Bad Gateway.
public sealed class SpeechSynthesisException(string message) : Exception(message);
