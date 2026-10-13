"""Resilient Dataverse API exercise - Dataverse SDK for Python.

Connects to Dataverse, then calls a helper that simulates one transient
error before the query succeeds. Add a bounded, Retry-After-aware retry
loop so the query survives that transient failure.
"""

import json
import time
from pathlib import Path

from azure.identity import InteractiveBrowserCredential
from PowerPlatform.Dataverse.client import DataverseClient
from PowerPlatform.Dataverse.core.errors import HttpError
from PowerPlatform.Dataverse.models import col

import flaky_query


def load_config():
    config_path = Path(__file__).parent / "config.json"
    with open(config_path, "r", encoding="utf-8") as config_file:
        return json.load(config_file)


def main():
    config = load_config()
    environment_url = config["dataverse_environment_url"]

    # Connect to Dataverse
    credential = InteractiveBrowserCredential()
    client = DataverseClient(environment_url, credential)

    # Confirm the sign-in succeeded
    employees = list(
        client.query.builder("eqp_employee")
        .select("eqp_name")
        .where(col("eqp_employeenumber") == "EMP-001")
        .execute()
    )
    print(f"Python sign-in succeeded. Found employee: {employees[0]['eqp_name']}")

    # Run the query with a bounded retry
    max_attempts = 3
    devices = None
    for attempt in range(1, max_attempts + 1):
        try:
            devices = flaky_query.query_issued_devices(client)
            break
        except HttpError as e:
            if e.is_transient and attempt < max_attempts:
                wait_seconds = e.details.get("retry_after", 2)
                print(
                    f"Attempt {attempt} hit a transient error "
                    f"(HTTP {e.status_code}). Retrying in {wait_seconds}s..."
                )
                time.sleep(wait_seconds)
            else:
                raise

    print(f"Found {len(devices)} issued device(s):")
    for device in devices:
        print(f"- {device['eqp_name']} ({device['eqp_devicetype']}) issued {device['eqp_issuedon']}")


if __name__ == "__main__":
    main()
