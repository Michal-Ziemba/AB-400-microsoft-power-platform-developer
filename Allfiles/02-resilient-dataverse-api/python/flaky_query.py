"""Supplied helper for the resilient Dataverse API exercise. Don't edit this file.

query_issued_devices() simulates one Dataverse throttling response (HTTP 429)
the first time it's called in a process, then runs the real query against
Dataverse on every call after that. This lets you prove a retry loop works
without having to generate real Dataverse throttling in a shared tenant.
"""

from PowerPlatform.Dataverse.core.errors import HttpError
from PowerPlatform.Dataverse.models import col

_has_raised = False


def query_issued_devices(client):
    """Returns issued devices and their issue dates.

    Raises a simulated transient HttpError (HTTP 429, with a retry_after
    value) on the first call. Every call after that runs the real query.
    """
    global _has_raised

    if not _has_raised:
        _has_raised = True
        raise HttpError(
            message="The availability service is temporarily unavailable. Try again later.",
            status_code=429,
            is_transient=True,
            retry_after=2,
            details={"retry_after": 2},
        )

    return list(
        client.query.builder("eqp_device")
        .select("eqp_name", "eqp_devicetype", "eqp_issuedon")
        .where(col("eqp_devicestatus") == 100000001)  # Issued
        .order_by("eqp_issuedon")
        .execute()
    )
