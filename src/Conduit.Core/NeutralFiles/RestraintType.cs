namespace Conduit.Core.NeutralFiles;

/// <summary>
/// CAESAR II's restraint type codes (1–62), as used in the <c>#$ RESTRANT</c> section and the
/// Restraints report ("Restraint Type/Tag" column, e.g. <c>Rigid ANC</c>, <c>Rigid +Y</c>,
/// <c>Rigid GUI</c>). v1's support-placement heuristic only ever assigns
/// <see cref="Anc"/>/<see cref="Y"/>/<see cref="Gui"/>/<see cref="Xspr"/> (rest, guide, and
/// spring-candidate placements), but the full table is reproduced here since any restraint
/// Conduit reads back out of a real file could carry any of these codes.
/// </summary>
public enum RestraintType
{
    /// <summary>Anchor — fully rigid in all six degrees of freedom.</summary>
    Anc = 1,

    /// <summary>Rigid translational restraint along X.</summary>
    X = 2,

    /// <summary>Rigid translational restraint along Y — v1's placement heuristic uses this for rest supports.</summary>
    Y = 3,

    /// <summary>Rigid translational restraint along Z.</summary>
    Z = 4,
    Rx = 5,
    Ry = 6,
    Rz = 7,

    /// <summary>Guide — restrains lateral translation, allows axial movement. Used for vertical-run supports.</summary>
    Gui = 8,

    /// <summary>Limit stop — restrains travel beyond a defined gap.</summary>
    Lim = 9,
    Xsnb = 10,
    Ysnb = 11,
    Zsnb = 12,
    PlusX = 13,
    PlusY = 14,
    PlusZ = 15,
    MinusX = 16,
    MinusY = 17,
    MinusZ = 18,
    PlusRx = 19,
    PlusRy = 20,
    PlusRz = 21,
    MinusRx = 22,
    MinusRy = 23,
    MinusRz = 24,
    PlusLim = 25,
    MinusLim = 26,
    Xrod = 27,
    Yrod = 28,
    Zrod = 29,
    PlusXrod = 30,
    PlusYrod = 31,
    PlusZrod = 32,
    MinusXrod = 33,
    MinusYrod = 34,
    MinusZrod = 35,
    X2 = 36,
    Y2 = 37,
    Z2 = 38,
    Rx2 = 39,
    Ry2 = 40,
    Rz2 = 41,
    PlusX2 = 42,
    PlusY2 = 43,
    PlusZ2 = 44,
    MinusX2 = 45,
    MinusY2 = 46,
    MinusZ2 = 47,
    PlusRx2 = 48,
    PlusRy2 = 49,
    PlusRz2 = 50,
    MinusRx2 = 51,
    MinusRy2 = 52,
    MinusRz2 = 53,

    /// <summary>Spring (variable-support) placement along X — v1 flags spring candidates with this family.</summary>
    Xspr = 54,
    Yspr = 55,
    Zspr = 56,
    PlusXsnb = 57,
    PlusYsnb = 58,
    PlusZsnb = 59,
    MinusXsnb = 60,
    MinusYsnb = 61,
    MinusZsnb = 62,
}
