# Nerubian 0.33 server parity

Reviewed patch sources/data for the existing Godot server pack. No credentials or account data. The six source overrides replace their .gd.remap entries when building a pack; keep all other game resources and current network/social telemetry. Validate in an isolated headless copy before any idle-gated deployment. Do not restart profiles/voice or replace account databases. This folder is source material, not a command or authorization to deploy.

Data: data-delta.json contains reviewed changed dictionary entries. Merge each data dictionary into its current full server JSON; merge aspects.races by race key. Preserve every unrelated entry and validate against the tested live baseline before packing. The local staging verification used full merged files, not these partial files as replacements.
