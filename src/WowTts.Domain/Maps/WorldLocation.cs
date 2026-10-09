namespace WowTts.Domain.Maps;

// A position in the game world. MapId 0 is the Eastern Kingdoms, 1 is Kalimdor, other IDs are instances.
// WoW's world X axis points north and Y points west, so X grows going north and Y grows going west.
public readonly record struct WorldLocation(int MapId, double X, double Y);
