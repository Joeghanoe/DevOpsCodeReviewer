# Code Review: Declaration - Setup Distancing & Remove Node API

**PR**: #8
**Repository**: Joeghanoe
**Author**: joeghanoe
**Generated**: 2026-01-20 20:07:56 UTC
**Total Comments**: 3

---

⚠️ **[ErrorHandling]** Ensure that the `Run` method validates the `DistanceRequest` prior to passing it to the service.

**Suggestion:** Validate the latitude and longitude values to ensure they represent valid geographical coordinates.

**Related:** Validation of input data, as outlined in methods handling sensitive domain logic.

<!-- DevOpsCodeReviewer -->

---

🚨 **[Documentation]** The `DistanceRequest` model lacks comments clarifying usage of its properties.

**Suggestion:** Add XML documentation comments to the properties specifying the expected formats and constraints.

**Related:** Providing structured documentation on DTOs and models used for APIs.

<!-- DevOpsCodeReviewer -->

---

⚠️ **[ErrorHandling]** Potential error swallowing in the `catch` block while calculating distance.

**Suggestion:** Log this error explicitly using the logging utility or library of the project for better traceability.

**Related:** Use `_logger.LogError(ex, "Error occurred while calculating distance.")` from `/api-dotnet/Functions/CalculateDistance.cs`.

<!-- DevOpsCodeReviewer -->

---

