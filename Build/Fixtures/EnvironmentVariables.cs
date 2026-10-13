using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace EquipmentProvisioning.Fixtures
{
    internal static class EnvironmentVariables
    {
        // Returns the current value of an environment variable, or its default value if no current value is set.
        public static string GetString(IOrganizationService service, string schemaName)
        {
            var query = new QueryExpression("environmentvariabledefinition")
            {
                ColumnSet = new ColumnSet("defaultvalue"),
                TopCount = 1,
            };
            query.Criteria.AddCondition("schemaname", ConditionOperator.Equal, schemaName);
            var value = query.AddLink("environmentvariablevalue", "environmentvariabledefinitionid", "environmentvariabledefinitionid", JoinOperator.LeftOuter);
            value.EntityAlias = "v";
            value.Columns = new ColumnSet("value");

            var definition = service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (definition == null)
            {
                return null;
            }
            var current = definition.GetAttributeValue<AliasedValue>("v.value")?.Value as string;
            return string.IsNullOrEmpty(current) ? definition.GetAttributeValue<string>("defaultvalue") : current;
        }
    }
}
