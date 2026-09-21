# Design System Assets

**Tarix:** 2026-04-29
**Status:** P1 canonical support spec
**Phase:** P1-core
**Scope:** Typography scale, spacing scale, border radius, animation tokens, reduced-motion və high-contrast policy, asset naming conventions və app/plugin icon resource rules
**Sources:** `NFR-019`, `NFR-027`, `AC-016`, `AC-027`, `AC-040`, `ADR-0028`, `ADR-0044`, `ADR-0046`, `ADR-0049`, `docs/specs/shell-ui.md`, `docs/specs/plugins-manager.md`

Bu spec design-system token-ları, asset naming, app/plugin icon resource qaydaları və theme color-token modelinin canonical owner-idir.

## 1. Sənədin məqsədi

Bu sənəd Dobby host shell, Admin Tool və plugin-lərin işlətdiyi design-system-level visual və motion token-larının canonical owner-idir: typography, spacing, border radius, animation, reduced motion, asset naming və app/plugin icon resource qaydaları. XAML resource dictionary-lər, theme faylları və asset folder-ları bu sənədin verdiyi input-ları izləməlidir.

## 2. Sənədin authority-si

- Bu spec aşağıdakıların canonical owner-idir: typography scale, spacing scale, border radius token-ları, animation timing/easing token-ları, reduced-motion policy, asset naming convention-ları, app və per-plugin icon resource qaydaları.
- `docs/specs/shell-ui.md` shell visual hierarchy və theming model üçün üstündür.
- Platform/accessibility ADR-ləri non-token qərarlar üçün üstündür.

## 3. Typography

| Token key | Value | Typical use |
|---|---|---|
| `Dobby.FontSize.Small` | `11` | secondary/status text, dense list metadata |
| `Dobby.FontSize.Body` | `13` | default body, labels, buttons, table cells |
| `Dobby.FontSize.Title` | `24` | screen/panel title |

- Default UI font is Segoe UI on Windows. Fallback chain: Segoe UI, Arial, sans-serif.
- Text must support Azerbaijani diacritics in visible labels, dialogs, validation messages and diagnostics exports.
- Body text uses normal weight; emphasis uses semibold, not all-caps.
- Screen/panel headings are concise and do not use hero-scale typography inside operational panels.
- Font size comes from these token keys; shell-level literal `FontSize` values are
  a release-gate failure (`docs/specs/shell-ui.md` § 6).
- Monospace is reserved for paths, IDs, command output and diagnostic snippets.

## 4. Spacing Scale

Spacing is a closed token set. Shell, built-in screens and host-rendered plugin
pages consume these keys; they do not introduce new base spacing values.

| Token key | Value | Typical use |
|---|---|---|
| `Dobby.Spacing.Tiny` | `3` | adjacent icon buttons, tab strip item margin, dense list rows |
| `Dobby.Spacing.Small` | `6` | control padding, panel inner padding, sidebar/third-bar insets |
| `Dobby.Spacing.Medium` | `12` | panel/section padding, toolbar and shell body margins |
| `Dobby.Spacing.Large` | `18` | wide section separation |

- The scale is a 3px base unit; `3`/`6`/`12`/`18` are the only base steps.
- Directional single-edge margin variants (bottom-only, right-only, top-only) may
  be declared as derived resources, but every component of such a variant must be
  a value from the base scale or a direct multiple of the base unit, and the
  variant must be declared as a named resource — never as an inline literal
  `Margin`/`Padding` on a screen element.
- Dense operational lists use `Tiny`/`Small`; cards, dialogs and settings groups
  use `Small`/`Medium` interior spacing.
- Adjacent controls in toolbars use stable spacing and must not shift when labels,
  hover states or progress indicators appear.
- Layout spacing is implemented through these resources, not hardcoded per-screen
  values. Shell-level hardcoded spacing is a release-gate failure
  (`docs/specs/shell-ui.md` § 6).

## 5. Border Radius

Border radius is a closed token set:

| Token key | Value | Typical use |
|---|---|---|
| `Dobby.Radius.Small` | `6` | inline inputs, small chips, compact controls |
| `Dobby.Radius.Medium` | `12` | panels, cards, dialogs, shell surfaces (Sidebar, Third Bar, Main Content, status/notification hosts) |
| `Dobby.Radius.Large` | `18` | pill/round icon buttons such as tab-strip new/close actions |

- Screens use these keys; per-screen literal `CornerRadius` values are not
  introduced at shell level.
- Repeated item cards, dialogs and framed tool surfaces may use radius tokens;
  page sections are not styled as nested decorative cards.
- Icon buttons keep stable square hit targets; hover/focus visuals do not resize
  the control, so a radius change never changes layout size.

## 6. Animation Tokens

| Token key | Value | Meaning |
|---|---|---|
| `Dobby.Motion.Fast` | `120ms` | default state-change duration for expand/collapse, transient feedback and focus handoff |
| `Dobby.Motion.Reduced` | `0ms` | substitution value used when motion is suppressed |

- Animations are short, purposeful and tied to state change: expand/collapse,
  progress, transient feedback and focus handoff.
- `Dobby.Motion.Fast` is the canonical default. A duration above `180ms` requires a
  specific UX reason recorded in a spec or ADR, not an inline screen value.
- Use ease-out for entrance/expansion and ease-in for exit/collapse.
- Loading indicators must not block keyboard navigation or resize their parent
  layout.

## 7. Reduced Motion

- The reduced-motion signal is the OS client-area animation preference exposed by
  WPF (`SystemParameters.ClientAreaAnimation`). When it is `false`, the host
  overwrites `Dobby.Motion.Fast` with the `Dobby.Motion.Reduced` value in the
  active resource dictionary, so every consumer of the shared token is suppressed
  at once and no screen needs its own reduced-motion branch.
- The substitution is fail-safe: if `Dobby.Motion.Reduced` is absent from the
  active dictionary, the host substitutes a zero duration rather than leaving the
  animated value in place.
- Reduced-motion mode disables decorative movement and replaces non-essential
  transitions with instant state changes. It never removes information: progress,
  busy and completion state stay visible through static text/indicator state.
- Reduced motion is an accessibility state, not a theme. It does not change layout,
  spacing, typography, radius, icon size, navigation model or screen composition,
  and it is applied for both light and dark themes.

## 7a. High Contrast

- The high-contrast signal is the OS high-contrast preference exposed by WPF
  (`SystemParameters.HighContrast`). When it is active, the host overrides the
  shared background, surface, accent, text and focus brushes with the
  corresponding Windows system brushes so that the OS palette wins over the Dobby
  palette.
- High-contrast overrides are applied to the shared token keys only. Screens do not
  detect high contrast themselves and do not define high-contrast-specific layout,
  control variants or per-screen palettes.
- Accessibility preferences are re-applied whenever the theme is applied, so a
  theme switch cannot silently restore animated durations or the non-high-contrast
  palette.
- The visible focus indicator survives both accessibility states: focus thickness
  stays at `Dobby.Focus.Thickness` and the focus brush remains distinguishable from
  the background (`NFR-019`, `AC-027`, `AC-040`).

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

Theme resource dictionary-ləri **eyni** token key set-ini export etməlidir: hər iki
dictionary-də (`LightTheme.xaml`, `DarkTheme.xaml`) aşağıdakı cədvəlin bütün
key-ləri mövcud olmalıdır və yeni token yalnız hər iki dictionary-ə birlikdə əlavə
olunur. Bir dictionary-də olub digərində olmayan brush key runtime-da theme
switch-dən sonra unresolved resource verir, ona görə asimmetrik token set qəbul
edilmir. Screen-lər theme-specific layout, theme-specific control variant və ya
per-screen dark palette yaratmır. Light theme approved shell direction-ın rəng
ailəsini saxlayır; dark theme Solarized Dark base və accent tonlarından törədilir.

Shell visual direction raster mockup faylı ilə canonical edilmir. Approved
direction Chrome-like workstation tab row, centered TopToolbar, icon rail Sidebar,
Main Content Area-nın yuxarı-mərkəzində yumşaq panel kəsiyi slotunda Third Bar və
continuous light shell background modelidir. Measurements, layout constants,
spacing scale, proportions, typography sizes, radius values, icon sizes və token
values yalnız bu spec-də və ya digər text spec-lərdə textual token/constant kimi
yazılanda canonical sayılır.

Navigation ordering, entry labels, plugin visibility/default state, measurements
və token dəyərləri `docs/specs/shell-ui.md` Navigation mapping bölməsi,
`docs/adr/ADR-0028-shell-composition-ordering-render.md`,
`docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md` və bu text
token spec-i tərəfindən idarə olunur. Visual reference asset-ləri runtime behavior,
permission behavior və release requirements üçün ADR/spec mətnini override etmir.

Canonical color token inventory. Token key-ləri `Dobby.Brush.*` namespace-indədir;
`Color.*` kimi paralel bir color-token adlandırması canonical deyil və shell
tərəfindən consume edilmir.

| Token key | Dark value | Solarized role (dark) | Light value |
|---|---|---|---|
| `Dobby.Brush.Background` | `#002B36` | `base03` | `#F7F7F2` |
| `Dobby.Brush.Surface` | `#073642` | `base02` | `#FFFFFF` |
| `Dobby.Brush.Panel` | `#0B3D48` | derived from `base02` | `#FDFDFB` |
| `Dobby.Brush.PanelTranslucent` | `#E60B3D48` | derived (alpha over panel) | `#EFFFFFFF` |
| `Dobby.Brush.ShellChrome` | `#07333B` | derived from `base03`/`base02` | `#EFEFEB` |
| `Dobby.Brush.Card` | `#124854` | derived elevation step | `#FFFFFF` |
| `Dobby.Brush.CardSoft` | `#0A333D` | derived recessed step | `#F5F6F2` |
| `Dobby.Brush.Border` | `#2B6370` | derived from `base01` | `#DADDD4` |
| `Dobby.Brush.Text` | `#EEE8D5` | `base2` | `#1C2424` |
| `Dobby.Brush.MutedText` | `#B7C7C8` | derived secondary text | `#6A726B` |
| `Dobby.Brush.Accent` | `#2AA198` | `cyan` | `#2F6F73` |
| `Dobby.Brush.Focus` | `#B58900` | `yellow` | `#8A6500` |
| `Dobby.Brush.Control` | `#0E424E` | derived control surface | `#F2F3EF` |
| `Dobby.Brush.ControlHover` | `#15505D` | derived hover step | `#E9ECE6` |
| `Dobby.Brush.ControlActive` | `#EEE8D5` | `base2` | `#050706` |
| `Dobby.Brush.ControlActiveText` | `#073642` | `base02` | `#FFFFFF` |
| `Dobby.Brush.ChartBlue` | `#7FB7DD` | derived from `blue` | `#7CA1C4` |
| `Dobby.Brush.ChartRose` | `#E58A8F` | derived from `red`/`magenta` | `#D96B72` |
| `Dobby.Brush.ChartGold` | `#E1C778` | derived from `yellow` | `#D8B45C` |

Derivation qaydası: `derived` sətirləri Solarized base/accent tonlarının
elevation, hover, recess və ya alpha addımıdır. Belə addım yalnız bu cədvəldə
yazıldıqda canonical olur; screen kodu öz elevation/hover variantını inline
literal rənglə yaratmır.

Status rəngləri hazırda shipped dictionary-lərdə token kimi mövcud deyil. Shell və
ya built-in ekran status rənginə ehtiyac göstərəndə token **hər iki** dictionary-ə
`Dobby.Brush.StatusSuccess`, `Dobby.Brush.StatusWarning`, `Dobby.Brush.StatusError`
adları ilə əlavə olunur və dark variantda aşağıdakı Solarized accent tonlarını
istinad kimi götürür; light variant eyni semantic rolu light rəng ailəsində
kontrast baseline-ı saxlayaraq doldurur:

| Semantic rol | Solarized reference tone |
|---|---|
| Success | `green` `#859900` |
| Warning | `yellow` `#B58900` |
| Error | `red` `#DC322F` |

Status rəngi per-screen literal kimi yazılmır və status məlumatı yalnız rənglə
çatdırılmır: mətn və ya icon state də olmalıdır (`NFR-019`, `AC-027`).

Hər iki theme `NFR-019` P1 accessibility baseline-ını ödəməlidir və baseline
ölçüləbilən minimumlarla bağlanır:

| Cütlük | Minimum contrast ratio |
|---|---|
| `Dobby.Brush.Background` / `Dobby.Brush.Text` | `4.5:1` |
| `Dobby.Brush.Surface` / `Dobby.Brush.Text` | `4.5:1` |
| `Dobby.Brush.Background` / `Dobby.Brush.Focus` | `3.0:1` |

Bir token konkret control state-də kontrast baseline-ını pozursa, düzəliş shared
color token-lərinin içində qalır: one-off screen override, per-screen palette və ya
inline literal rəng ilə həll edilmir.

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

Source asset delivery requirements:

- App icon: transparent multi-size `.ico` containing at least 16, 32, 48, 64, 128 and 256 px variants.
- App logo: transparent PNG, minimum 256 px source.
- Navigation icon: 20 px visual target.
- Toolbar icon: 16 px visual target.
- Status icon: 12 px visual target.
- Dialog icon: 24 px visual target.

Runtime icon sizing is a token, not a per-asset literal. The shell exposes a single
icon size token `Dobby.Icon.Size` (`16`), and vector/raster icons are rendered
inside a size-constrained container bound to that token so an oversized source
asset cannot change layout. The delivery sizes above are source-asset production
guidance; when a surface needs a visual target other than `Dobby.Icon.Size`, the
required size is added here as a named token and consumed by key — it is not
written as an inline literal width/height on a screen element. Fixed-size brand
marks are the only permitted exception and must stay outside interactive icon
slots.

### Shell measurement tokens

Shell chrome measurements are canonical here as text tokens
(`docs/specs/shell-ui.md` defers measurements, spacing scale, proportions,
typography sizes, radius values and icon sizes to this spec):

| Token key | Value | Meaning |
|---|---|---|
| `Dobby.Shell.ChromeHeight` | `48` | shell chrome/tab-row band height |
| `Dobby.Shell.TabWidth` | `240` | workstation tab maximum width |
| `Dobby.Shell.TabHeight` | `42` | workstation tab height |
| `Dobby.Shell.CaptionButtonReserveWidth` | `144` | reserved width for native caption buttons |
| `Dobby.Shell.CaptionButtonReserveGridLength` | `144` | grid-length form of the same reserve, kept equal to the width token |
| `Dobby.Shell.CaptionButtonWidth` | `46` | single caption button width |
| `Dobby.Shell.CaptionButtonHeight` | `32` | single caption button height |
| `Dobby.Shell.NativeCaptionFrameThickness` | `0,48,0,0` | client-area inset that keeps content clear of the native caption band; top value tracks `Dobby.Shell.ChromeHeight` |
| `Dobby.Focus.Thickness` | `2` | visible focus/border stroke thickness |

The caption-button reserve must remain at least three caption button widths so the
tab row can never overlap native minimize/maximize/close hit targets, and the
caption frame inset top value must equal `Dobby.Shell.ChromeHeight`. These two
relationships are invariants, not incidental values: changing one token without the
other produces overlapping or clipped chrome.

Host-owned icon və theme resursları shared resource dictionary ownership altında saxlanmalıdır.

Plugin resource ownership host central resource namespace-dən ayrıdır. Plugin resource key-ləri plugin package/assembly boundary-sində qalmalı, host `Dobby.Localization` central resource file-larına köçürülməməli və ADR-0044 host naming convention-u ilə qarışdırılmamalıdır.

Concrete plugin prefix syntax canonical edilmir; belə syntax lazımdırsa ADR-0044 amendment və ya implementation round-da ayrıca bağlanmalıdır. Bu qayda runtime plugin SVG loader açmır və plugin physical resource/package folder convention-u bu spec-də bağlamır.

## 10. References

- Sections 3-8 — typography, spacing, border radius, animation and reduced-motion tokens.
- Section 7a — high-contrast accessibility override policy.
- Section 8 — asset naming conventions.
- Section 9 — application və plugin icon resource boundary.
- Section 9.1 — unified theme design, non-normative visual reference boundary və canonical `Dobby.Brush.*` color token inventory.
- Section 9.2 — `Dobby.UI` host/built-in implementation inventory.
- `Asset delivery tokens` / `Shell measurement tokens` — source asset delivery sizes, runtime icon size token və shell chrome measurement tokens.
- `docs/specs/plugins-manager.md` (plugin icon validation and fallback behavior)
- `docs/specs/shell-ui.md` (shell visual hierarchy və theming model)
- `docs/06_NON_FUNCTIONAL_REQUIREMENTS.md` — `NFR-019`, `NFR-027`
- `docs/07_ACCEPTANCE_CRITERIA.md` — `AC-016`, `AC-027`, `AC-040`
- `docs/adr/ADR-0028-shell-composition-ordering-render.md`
- `docs/adr/ADR-0044-localization-resource-organization.md`
- `docs/adr/ADR-0049-hierarchical-navigation-contribution-model.md`

## 11. Verification Obligations

`NFR-027` behavior-first proof rule bu spec-ə də tətbiq olunur: token faylında
string-in mövcudluğunu yoxlayan source-text scan design-system tələbini
qapatmır. Aşağıdaki obligation-lar ya parsed/computed dəyər üzərində, ya da
rendered surface üzərində sübut olunmalıdır.

| Obligation | Minimum proof |
|---|---|
| Theme key symmetry | `LightTheme.xaml` və `DarkTheme.xaml` parse olunur və brush key set-lərinin **eyni** olduğu müqayisə edilir; asimmetriya fail-dir. |
| Contrast baseline | § 9.1 cədvəlindəki üç cütlük hər iki theme üçün parse olunmuş rəng dəyərlərindən hesablanmış contrast ratio ilə yoxlanılır. |
| Reduced motion | `Dobby.Motion.Fast` dəyərinin OS animation preference `false` olduqda reduced dəyərlə əvəzləndiyi resource dictionary üzərində behavioral çağırışla yoxlanılır. |
| High contrast | high-contrast aktiv olduqda background/surface/accent/text/focus brush-larının system brush-larla əvəzləndiyi eyni behavioral çağırışla yoxlanılır. |
| Fail-safe substitution | `Dobby.Motion.Reduced` dictionary-də olmadıqda zero duration substitution-ın işlədiyi yoxlanılır. |
| Token-only styling | rendered shell surface-də spacing/radius/font/color dəyərlərinin shared resource-lardan gəldiyi yoxlanılır; shell-level inline literal color/spacing/font release gate-də fail verir. |
| Shell measurement invariants | caption reserve ≥ üç caption button width və caption frame inset top = `Dobby.Shell.ChromeHeight` bərabərliyi parse olunmuş token dəyərləri üzərində yoxlanılır. |
| Plugin icon fallback | invalid/missing/oversized plugin icon-un default icon-a düşdüyü və raw path/decoder detalının user-facing səthə çıxmadığı runtime install/render axını ilə yoxlanılır (`docs/specs/plugins-manager.md`). |

## Revision History

- **2026-08-27:** Token reconciliation. Bu spec measurement/token authority-si olduğu
  halda mətn dəyərləri shipped və approved shell token dictionary-ləri ilə ziddiyyətli
  idi; §§ 3–7 və § 9.1 canonical token key-ləri (`Dobby.FontSize.*`, `Dobby.Spacing.*`,
  `Dobby.Radius.*`, `Dobby.Motion.*`, `Dobby.Brush.*`) və onların dəyərləri ilə
  yenidən yazıldı. Repository-də heç bir yerdə consume olunmayan `Color.*` paralel
  token adlandırması ləğv edildi; status rəngləri "mövcud token" kimi göstərilmək
  yerinə explicit "hər iki dictionary-ə əlavə olunmalı" qaydası ilə bağlandı.
  Reduced-motion hedge-i (`where the WPF stack exposes them`) konkret OS signal,
  token substitution və fail-safe qaydası ilə əvəz olundu; implementasiyada mövcud
  olub sənədləşdirilməmiş high-contrast override policy § 7a kimi əlavə edildi;
  sənədləşdirilməmiş shell chrome measurement token-ları `Shell measurement tokens`
  bölməsində canonical edildi; `Sources`, `Phase`, § 11 `Verification Obligations` və
  bu Revision History bölməsi əlavə olundu.
