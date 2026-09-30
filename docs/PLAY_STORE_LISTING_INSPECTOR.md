# Google Play Store Listing Inspector & Publisher API Extension

## 1. Overview

`Hydra.Vitals` now provides direct programmatic inspection and extraction of Google Play Store Listings via the Google Play Android Publisher API (Edits API v3).

This extends the previous store synchronization capabilities (`PlayStoreSyncService`) by adding:
1. `GetFullListingDetailsAsync`: Queries full listing metadata including Title, Short Description, Full Description, Video URL, and all associated media (Icon, Feature Graphic, Phone Screenshots, 7-inch & 10-inch Tablet Screenshots).
2. Direct CLI integration (`--store-listing <package_name> [--lang <language_code>]`).
3. Automated token reauthorization with loopback flow.

## 2. Command Line Interface (CLI)

```bash
# List all languages currently configured in Google Play Store
dotnet run --project Hydra.Vitals.csproj -- --list-langs com.arargames.spacedodger

# Query default English store listing for Space Dodger
dotnet run --project Hydra.Vitals.csproj -- --store-listing com.arargames.spacedodger

# Query specific localized language listing (e.g., en-US, tr-TR, fa)
dotnet run --project Hydra.Vitals.csproj -- --store-listing com.arargames.spacedodger --lang fa

# Batch upload & commit multi-language listings from JSON
dotnet run --project Hydra.Vitals.csproj -- --upload-listings com.arargames.spacedodger path/to/listings.json --commit

# List all accessible apps in Google Play Console
dotnet run --project Hydra.Vitals.csproj -- --list-apps

# Force re-authentication / OAuth renewal
dotnet run --project Hydra.Vitals.csproj -- --reauth
```

## 3. Architecture & SOLID Principles

- **Single Responsibility (SRP):** `PlayPublisherClient` manages raw Google Play Publisher Edits API calls. `StoreListingDetails` encapsulates the complete listing model.
- **Dependency Inversion (DIP):** Relies on `IGoogleTokenService` for secure DPAPI-backed token rotation and automatic browser loopback reauthorization.
- **Open/Closed (OCP):** Non-destructive edits: queries are executed inside a temporary Edit session (`InsertEditAsync`) and safely cleared with `DeleteEditAsync`, guaranteeing zero unintentional mutations to production listings.
