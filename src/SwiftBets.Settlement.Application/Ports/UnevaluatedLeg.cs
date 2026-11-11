using SwiftBets.Settlement.Domain;

namespace SwiftBets.Settlement.Application.Ports;

public sealed record UnevaluatedLeg(IndexedLeg Leg, FixtureResult Result);
