# Design System Assets

**Tarix:** 2026-04-29
**Status:** P1 canonical support spec
**Scope:** Typography scale, spacing scale, border radius, animation tokens, reduced-motion policy, asset naming conventions və app/plugin icon resource rules

Bu spec design-system token-ları, asset naming, app/plugin icon resource qaydaları və theme color-token modelinin canonical owner-idir.

## 1. Sənədin məqsədi

Bu sənəd Dobby host shell, Admin Tool və plugin-lərin işlətdiyi design-system-level visual və motion token-larının canonical owner-idir: typography, spacing, border radius, animation, reduced motion, asset naming və app/plugin icon resource qaydaları. XAML resource dictionary-lər, theme faylları və asset folder-ları bu sənədin verdiyi input-ları izləməlidir.

## 2. Sənədin authority-si

- Bu spec aşağıdakıların canonical owner-idir: typography scale, spacing scale, border radius token-ları, animation timing/easing token-ları, reduced-motion policy, asset naming convention-ları, app və per-plugin icon resource qaydaları.
- `docs/specs/shell-ui.md` shell visual hierarchy və theming model üçün üstündür.
- Platform/accessibility ADR-ləri non-token qərarlar üçün üstündür.

## 3. Typography

- Default UI font is Segoe UI on Windows. Fallback chain: Segoe UI, Arial, sans-serif.
- Text must support Azerbaijani diacritics in visible labels, dialogs, validation messages and diagnostics exports.
- Body text uses normal weight; emphasis uses semibold, not all-caps.
- Screen/panel headings are concise and do not use hero-scale typography inside operational panels.
- Monospace is reserved for paths, IDs, command output and diagnostic snippets.

## 4. Spacing Scale

- Spacing uses a 4px base grid with common steps: `4`, `8`, `12`, `16`, `24`, `32`.
- Dense operational lists may use `4`/`8`; cards, dialogs and settings groups use `12`/`16` interior spacing.
- Adjacent controls in toolbars use stable spacing and must not shift when labels, hover states or progress indicators appear.
- Layout spacing is implemented through reusable resources/tokens, not hardcoded per-screen values.

## 5. Border Radius

- Standard controls and cards use radius no greater than 8px unless the shell design reference explicitly requires otherwise.
- Repeated item cards, dialogs and framed tool surfaces may use radius tokens; page sections are not styled as nested decorative cards.
- Icon buttons keep stable square hit targets; hover/focus visuals do not resize the control.

## 6. Animation Tokens

- Animations are short, purposeful and tied to state change: expand/collapse, progress, transient feedback and focus handoff.
- Default duration range is `120ms` to `180ms`; longer animations require a specific UX reason.
- Use ease-out for entrance/expansion and ease-in for exit/collapse.
- Loading indicators must not block keyboard navigation or resize their parent layout.

## 7. Reduced Motion

- The host honors OS reduced-motion preferences where the WPF stack exposes them.
- Reduced-motion mode disables decorative movement and replaces non-essential transitions with instant state changes.
- Progress indication remains visible in reduced-motion mode through static text/indicator state.

## 8. Asset Naming

- Asset file names use stable kebab-case names: `plugin-default.png`, `app-icon.ico`, `settings-gear.png`.
- Asset names describe semantic purpose, not temporary color or size.
- Raster assets include scale/size variant only when needed by the platform pipeline.
- Plugin-provided assets are copied/validated through host package validation; shell code does not reference arbitrary plugin file paths.

## 9. Application və Plugin Icons

- Host app icon and logo are host-owned resources.
- Shell/built-in icons use the approved WPF icon system or host-owned raster assets.
- Plugin icons follow `docs/specs/plugins-manager.md`: PNG source, minimum `64x64`, host validation and default fallback icon.
- Runtime plugin SVG loading is not introduced without a separate security/design decision.
- Missing, invalid, unsupported or unsafe plugin icons fall back to the default plugin icon without exposing raw path/decoder details to the user.

### 9.1 Theme System And Dark Palette

Dobby theme sistemi vahid UI design üzərində qurulur. Light, dark və gələcək theme variantları eyni layout, spacing scale, typography, border radius, icon size, motion token, navigation structure və screen composition istifadə edir; theme dəyişəndə yalnız semantic color/brush token-ləri dəyişir.

Theme resource dictionary-ləri eyni token adlarını export etməlidir. Screen-lər theme-specific layout, theme-specific control variant və ya per-screen dark palette yaratmır. Light theme `ui_design_light_theme.jpg` visual direction-ının rəng ailəsini saxlayır; dark theme hazırkı canonical rəng seti kimi Solarized Dark palette-dən istifadə edir.

`ui_design_light_theme.jpg` Dobby-owned light theme visual reference-dir. Asset
light-theme visual direction üçün support verir, amma measurements, layout
constants, spacing scale, proportions, typography sizes, radius values, icon sizes
və token values üçün təkbaşına normative authority deyil. Bu dəyərlər yalnız bu
spec-də və ya digər text spec-lərdə textual token/constant kimi yazılanda canonical
sayılır. Asset business-domain labels, customer data və third-party branding
daşımır.

Asset navigation ordering, entry labels, plugin visibility/default state,
measurements və token dəyərləri üçün canonical deyil; bu qaydalar
`docs/specs/shell-ui.md` Navigation mapping bölməsi,
`docs/adr/ADR-0028-shell-composition-ordering-render.md`,
`docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md` və bu text
token spec-i tərəfindən idarə olunur. Visual reference asset-ləri runtime behavior,
permission behavior və release requirements üçün ADR/spec mətnini override etmir.

Solarized Dark token mapping:

| Token | Solarized role | Hex |
|---|---|---|
| `Color.Background.Surface` | `base03` | `#002B36` |
| `Color.Background.Panel` | `base02` | `#073642` |
| `Color.Background.Elevated` | `base02` | `#073642` |
| `Color.Text.Primary` | `base0` | `#839496` |
| `Color.Text.Strong` | `base1` | `#93A1A1` |
| `Color.Text.Secondary` | `base01` | `#586E75` |
| `Color.Border.Subtle` | `base01` | `#586E75` |
| `Color.Action.Primary` | `blue` | `#268BD2` |
| `Color.Action.Secondary` | `cyan` | `#2AA198` |
| `Color.Status.Success` | `green` | `#859900` |
| `Color.Status.Warning` | `yellow` | `#B58900` |
| `Color.Status.Error` | `red` | `#DC322F` |
| `Color.Accent.Optional` | `violet` | `#6C71C4` |

Dark theme contrast must satisfy the P1 accessibility baseline in `NFR-019`; if a Solarized token fails contrast in a specific control state, the fix must remain inside shared color tokens and must not create a one-off screen override.

### 9.2 Dobby.UI Host/Built-in Implementation Inventory

`Dobby.UI` minimum inventory host/shell implementation scope-udur. Bu siyahı
plugin-visible public/UI contract deyil və plugin SDK-nın komponent kitabxanası
kimi expose edilmir.

| Group | Inventory | Scope |
|---|---|---|
| Shell basis | WPF-UI `NavigationView`, `TitleBar`, `Snackbar`, `Dialog` | Shell composition, title/chrome, transient feedback və modal/dialog hosting üçün istifadə olunur. |
| Built-in form controls | `DTextBox`, `DPasswordBox`, `DButton`, `DCheckBox` | Login/auth, Settings, Plugins Manager və built-in forms üçün shared styling/behavior wrapper-ları. |
| Lists/status | WPF built-in `DataGrid`, `DText`/`DLabel`, `DProgressIndicator` | Plugin list, settings/user-management tables, status/progress display və diagnostics/support-safe text surfaces. |
| P1-if-time controls | `DComboBox`, `DDatePicker`, `DRadioButton`, `DToast` or `DNotification` | Yalnız built-in screens real ehtiyac göstərərsə P1-if-time; plugin-visible contract yaratmır. |

Inventory qaydaları:

- `D*` controls Dobby host/built-in WPF wrapper naming-dir; plugin manifest və
  plugin page contracts bu control adlarını daşımır;
- WPF-UI controls shell basis üçün istifadə oluna bilər, amma `Dobby.UI` onları
  plugin-ə public dependency kimi expose etmir;
- DataGrid P1 üçün WPF built-in DataGrid + shared styling ilə başlayır; commercial
  grid və ya open-ended grid package əlavə olunmur;
- P1-if-time controls skeleton-da yalnız istifadə edən built-in screen olduqda
  yaradılır; boş wrapper project-i şişirtmək üçün yaradılmır.

## Asset delivery tokens

- App icon: transparent multi-size `.ico` containing at least 16, 32, 48, 64, 128 and 256 px variants.
- App logo: transparent PNG, minimum 256 px source.
- Navigation icon: 20 px visual target.
- Toolbar icon: 16 px visual target.
- Status icon: 12 px visual target.
- Dialog icon: 24 px visual target.

Host-owned icon və theme resursları shared resource dictionary ownership altında saxlanmalıdır.

Plugin resource ownership host central resource namespace-dən ayrıdır. Plugin resource key-ləri plugin package/assembly boundary-sində qalmalı, host `Dobby.Localization` central resource file-larına köçürülməməli və ADR-0044 host naming convention-u ilə qarışdırılmamalıdır.

Concrete plugin prefix syntax canonical edilmir; belə syntax lazımdırsa ADR-0044 amendment və ya implementation round-da ayrıca bağlanmalıdır. Bu qayda runtime plugin SVG loader açmır və plugin physical resource/package folder convention-u bu spec-də bağlamır.

## 10. References

- Sections 3-8 — typography, spacing, border radius, animation, reduced motion and asset naming tokens.
- Section 9 — application və plugin icon resource boundary.
- Section 9.1 — unified theme design, non-normative visual reference boundary and Solarized Dark color-token mapping.
- Section 9.2 — `Dobby.UI` host/built-in implementation inventory.
- `docs/specs/plugins-manager.md` (plugin icon validation and fallback behavior)
- `docs/specs/shell-ui.md` (shell visual hierarchy və theming model)
