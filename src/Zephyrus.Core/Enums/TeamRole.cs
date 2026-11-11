namespace Zephyrus.Core.Enums;

/// <summary>
/// The three amplified roles Zephyrus is designed around. A single person may
/// hold several roles, but the approval responsibilities stay distinct.
/// </summary>
public enum TeamRole
{
    /// <summary>Product &amp; Engineering Manager — vision and delivery.</summary>
    PmEm,

    /// <summary>Tech Lead — architecture and code.</summary>
    TechLead,

    /// <summary>QA Engineer — quality and correctness.</summary>
    Qa
}
