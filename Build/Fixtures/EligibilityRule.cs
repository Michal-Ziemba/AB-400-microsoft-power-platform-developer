using System;

namespace EquipmentProvisioning.Fixtures
{
    // R1, the replacement eligibility rule (foundation spec §3, confirmed by the SME):
    // a device can be replaced if it's damaged beyond repair, if it has no recorded issue date,
    // or if it was issued on or before today minus 24 calendar months.
    public static class EligibilityRule
    {
        public static bool IsEligible(DateTime? issuedOn, bool damagedBeyondRepair, DateTime today)
        {
            if (damagedBeyondRepair)
            {
                return true;
            }
            if (!issuedOn.HasValue)
            {
                return true;
            }
            return issuedOn.Value.Date <= today.Date.AddMonths(-24);
        }
    }
}
