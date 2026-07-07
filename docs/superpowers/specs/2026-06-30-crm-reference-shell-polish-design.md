# CRM Reference Shell Polish Design

## Goal

Polish the Dobby shell so it visually follows the provided CRM reference while preserving the already-approved workstation tab behavior from `2026-06-29-shell-workstation-tabs-design.md`.

The current shell behavior is mostly correct, but the rendered UI still feels like raw WPF controls. This design turns the shell into a cohesive CRM-style desktop surface: soft light background, Chrome-like workstation tabs, compact icon navigation, centered module navigation, translucent work panels, and dense card-based content.

## Non-Negotiable Existing Decisions

This design does not reopen the workstation tab model.

- Workstations render as Chrome-like tabs in the top titlebar row.
- Startup creates one active Home workstation.
- The `+` button opens a new active Home workstation.
- Each workstation owns its current route and title.
- Switching workstations restores that workstation's route.
- Closing the last workstation replaces it with a fresh Home workstation.
- The window `X` closes the whole app after confirmation; it does not close only the active workstation.
- Work-session warning and minimize/close rules remain owned by the existing work-session coordinator.

## Visual Target

The target visual language is the provided SugarCRM-style reference, adapted to Dobby:

- one continuous soft light shell background;
- a slim left icon rail with proper vector icons, not placeholder glyphs;
- a top CRM navigation row with compact pill states;
- a large rounded/translucent main work panel;
- compact pipeline columns and cards inside the work panel;
- bottom metric panels with chart/gauge-style content;
- subtle depth through surfaces and spacing, not heavy shadows or decorative blobs.

The shell should look like an intentional product UI, not a diagnostic/control scaffold.

## Workstation Chrome

The topmost row is the workstation chrome. It visually follows browser tabs:

- active workstation tab has a rounded top shape and blends into the shell surface below;
- inactive workstation tabs sit behind the active one with softer fill;
- each tab shows a small icon, title, and close action;
- `+` sits immediately after the visible workstation tabs;
- window minimize/maximize/close controls stay on the far right;
- the row remains light, not a dark browser strip.

Icons must be proper vector icons. Placeholder text glyphs such as `[]`, `x`, `◆`, `⌘`, or raw order numbers must not be the final visual style.

## Main Navigation And Sidebar

The Dobby brand lives below the workstation chrome in the shell header, aligned with the reference's top-left product mark. The primary module navigation is centered in the top row and uses compact pill states.

The left sidebar is an icon rail. It should feel like part of the same continuous shell background, not a separate block panel. The active sidebar item may use a dark circular/pill highlight. Expanded sidebar label support can remain available for the existing collapsed/expanded model, but the polished default should resemble the reference's slim icon rail.

## Pipeline Subnav Placement

The secondary module controls, such as `Pipelines`, `Davam edir`, `Tamamlandı`, and `Vaxtı keçib`, sit in the third visual zone of the main work panel.

This zone uses the approved `dobby-reference-shell-v5-reference-notch.html` visual companion:

- the controls sit inside the main work panel's upper-center soft cutout slot;
- the main panel remains one continuous rounded surface; the cutout is created by a rounded background-colored mask, not by separate left/right chunks;
- the controls overlap the work panel edge while the panel chrome remains visually continuous around the cutout;
- the active control is a black pill;
- inactive controls are light pills with minimal treatment.

Do not replace the approved soft panel cutout placement with a floating island, a hard rectangular split, or a plain loose row of controls on the shell background.

## Content Surface

The Home route should no longer be an empty white panel with scattered labels. It should show a CRM-like dashboard surface using existing Dobby concepts:

- page heading;
- case/work pipeline section;
- four status columns;
- compact cards with avatar/icon, title, metadata, and status/action icon;
- bottom metric panels.

The content can use static sample shell data for first-pass visual structure as long as it is clearly internal shell presentation and does not pretend to be persisted business data.

Plugins and Settings should use the same visual system even if their internals remain simpler in the first implementation pass.

## Component Boundaries

Keep the already-approved shell model boundaries:

- `ShellViewModel` owns workstation tab state, route state, and presentation lists.
- WPF XAML owns layout and visual templates.
- Theme dictionaries own colors, radii, spacing, focus, and control styles.
- Work-session lifecycle remains outside visual polish code.

Add visual structure through styles/templates and small presentation models where needed. Do not hardcode strings or one-off colors directly into `MainWindow.xaml`.

## Accessibility And Localization

The polished design must remain keyboard and screen-reader usable:

- workstation tabs keep localized accessible names;
- icon-only actions have localized automation names/tooltips;
- focus indicators remain visible against the light shell;
- text is localized through existing resource keys;
- contrast remains above the existing accessibility baseline.

## Testing

Add tests and guardrails at the level appropriate to the risk:

- static scan prevents raw default-looking shell controls from reappearing where styled workstation tabs/navigation controls are required;
- rendered WPF smoke checks confirm workstation chrome, sidebar rail, top module nav, main work panel, subnav controls, and content cards render with nonzero size;
- existing workstation behavior tests continue to pass unchanged;
- theme contrast tests continue to pass;
- visual verification includes launching the app and checking the shell after login.

## Acceptance Criteria

- The shell opens with a Chrome-like workstation tab row at the top.
- The shell uses the CRM reference's light, soft, compact visual language.
- Logo and navigation symbols are proper icons, not placeholder characters.
- The main panel contains a polished dashboard/pipeline presentation instead of an empty white content area.
- The secondary subnav sits in the approved upper-center soft cutout slot of the main work panel, with the panel rendered as one continuous rounded surface.
- The active secondary subnav item is a black pill; inactive items are light pills.
- Existing workstation behavior remains unchanged and covered by existing tests.
- No visible UI regresses to raw WPF default button styling in the main shell.
