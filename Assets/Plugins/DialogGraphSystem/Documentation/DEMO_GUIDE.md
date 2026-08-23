# Dialogue Graph System Demo Guide

Open `Assets/DialogGraphSystem/DemoScenes/DialogueDemo.unity`, enter Play Mode, and choose one of the three cards. The scene resets dialogue variables and all action-feedback objects before every run. Use the persistent controls to return to the menu, reset and replay the current demo, or open Runtime Settings without reopening the scene.

## Product Tour

- Graph: `Graphs/Demo_ProductTour.asset`
- Characters: Dax and Maeve
- Environment: Neon Rooftop
- Variable: `playerName`
- Actions: `TurnOnTV`, `FadeLights`
- Outcomes: `product_tour_branching`, `product_tour_actions`

This 60-90 second route introduces speaker changes, localized variable text, two meaningfully different choices, visible scene feedback, and named outcomes. Use the runtime toolbar to inspect history/backlog, autoplay, skip, text presentation, and settings.

Run both choices from a clean state with **Reset / Re-run**. Open **Settings**, switch Language between English and Deutsch, then replay either route; localization changes presentation without changing graph flow.

## Shop Gate

- Graph: `Graphs/Demo_ShopGate.asset`
- Characters: Sol and Veya
- Environment: Forest Checkpoint
- Variables: `gold`, `hasKey`, `trustLevel`, `reputation`
- Action: `OpenGate`
- Outcomes: payment, key, trust, insufficient-gold, missing-key, and denied-trust results

This 60-120 second checkpoint negotiation demonstrates variable injection, player choices, Condition nodes, Set Variable nodes, success and denial, and explicit outcomes. Payment succeeds from the default 20 gold. Enable **Start with checkpoint key** or **Start trusted** on the card to make those successful routes available. Leave both disabled to inspect their denial routes.

Every successful route opens the gate exactly once. Denied routes never open it. The state readout shows the current values, and every reset restores `gold = 20`, `hasKey = false`, `trustLevel = false`, and `reputation = 1` before applying the selected route setup.

## Reactor Control Room

- Entry graph: `Graphs/Demo_ControlRoomActions.asset`
- Supporting graph: `Graphs/Demo_ReactorAftermath.asset`
- Characters: Dax and Sol
- Environment: Reactor Control Room
- Variables: `temperature`, `questState`
- Actions: `TurnOnTV`, `FadeLights`, `PlayAlarm`, `StartCountdown`, `OpenGate`
- Outcomes: `reactor_contained`, `reactor_unstable`

This 90-150 second demo contrasts calm containment with an emergency response. The calm route turns on the display, dims the room, and reduces temperature below the safe threshold. The emergency route turns on the display, plays the alarm, runs the countdown, opens the containment gate in the support graph, and returns to a failed final safety check.

Both routes enter the same supporting Graph Jump target by direct asset reference and stable graph identity. `Demo_ReactorAftermath` is intentionally registered for runtime resolution but is not a fourth menu entry. Reset restores temperature to `96`, state to `standby`, and clears the TV, dimmer, alarm, countdown, and gate.

## Inspect The Authored Graphs

1. Exit Play Mode.
2. Open `Tools > BekaForge > Dialogue Graph System > Graphs`.
3. Open one of the three `demo-entry` graphs listed above.
4. Use the graph metadata and categories to identify each demo's purpose.
5. Run validation and inspect Start, Dialog, Choice, Action, Condition, Set Variable, Graph Jump, Outcome, and End nodes across the catalog.
6. Open `Demo_ReactorAftermath` to inspect the non-selectable modular continuation.

The matching JSON exports are under `Assets/DialogGraphSystem/Resources/JSON`. English and German strings are in the two localization tables under `Definitions/Localization`.

## Action Feedback Objects

| Action ID | Scene receiver | Visible or audible result |
|---|---|---|
| `TurnOnTV` | `ActionShowcase` -> `TV` | Swaps the display from off to on. |
| `FadeLights` | `ActionShowcase` -> `SceneDimOverlay` | Fades the scene dimmer to the requested intensity. |
| `PlayAlarm` | `ActionShowcase` -> `AlarmOverlay` and `AlarmAudioSource` | Pulses a warning overlay and plays the configured alarm audio. |
| `StartCountdown` | `ActionShowcase` -> `CountdownRoot` | Shows a HUD countdown and completion state. |
| `OpenGate` | `ActionShowcase` -> `Door` | Slides the gate at the requested speed. |

If an action appears not to respond, select `ActionShowcase` and verify those serialized references plus the `DialogActionRunner` handler binding. Use **Reset / Re-run** before retesting so no prior visual state leaks into the route.
