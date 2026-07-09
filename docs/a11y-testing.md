# Accessibility testing

What this repo implements and what CI verifies on every push, stated precisely so the
claim is checkable:

| Layer | What it verifies | Where it runs |
|---|---|---|
| Behavioral suite (Playwright, Chromium) | Focus trapping, roving focus, keyboard maps, dismissal, ARIA state transitions per the published keyboard tables | `tests/e2e`, every push/PR in CI |
| axe-core sweep (WCAG 2.1 A/AA) | Names, roles, states, ARIA reference validity, contrast on every playground route | `tests/e2e/specs/axe.spec.ts`, every push/PR in CI |

Documentation states what components implement and what CI verifies; it does not
make conformance claims.

---

## Optional manual passes

The NVDA and VoiceOver runbooks below are reference checklists for a manual
screen-reader pass. They are **not** part of the release process, are not run in CI,
and have not been performed against this codebase. Treat the following sections as a
guide for anyone who chooses to run them, not a record of results.

### NVDA pass (Windows)

Setup: NVDA (free, nvaccess.org), Chrome and Firefox, the playground running locally (`dotnet run --project playground/Navius.Playground`). Use speech viewer (NVDA menu > Tools > Speech Viewer) to log announcements. Test with the browser maximized and the mouse untouched: the pass is keyboard-only by definition.

Rules of the pass:

- Every flow must be completable without sight: if you need to look at the screen to know where you are, that is a finding.
- Record findings as component / step / expected announcement / actual announcement.
- Do the pass twice: browse mode navigation (arrow keys, quick nav) and focus mode interaction (Tab, arrows, Enter, Space, Escape).

#### Per-family checklist

**Dialog / AlertDialog / Drawer**
- Trigger announces name + "button". On open: "dialog" (or "alert dialog"), the Title as the accessible name, Description if present.
- Focus lands inside (AlertDialog: on the Cancel action); Tab cycles inside only; background content is not reachable in browse mode while open.
- Escape closes and NVDA reads the trigger again (focus returned).

**Menu / Menubar / ContextMenu**
- Trigger: name + "menu button" + collapsed/expanded state. Open with Enter, Space, ArrowDown (first item focused), ArrowUp (last item focused).
- Items announce as "menu item" with position where supported; checkbox items announce checked/unchecked/mixed; radio items announce selected state.
- Typeahead moves focus and NVDA reads the newly focused item. Submenu triggers announce "submenu"/collapsed. Escape closes level by level and returns focus.

**Select / Combobox / Autocomplete**
- Announces its label (never just "combobox"), collapsed/expanded, current value.
- Arrowing through options reads each option and its selected state without DOM focus leaving the input (Combobox/Autocomplete: activedescendant model; verify NVDA tracks the highlight).
- Filtering announces result availability; multiselect chip removal announces what was removed.

**Form fields (Input, MaskedInput, CurrencyInput, DateInput, TimeInput, OTP)**
- Every control announces its label. Date/time segments announce as labelled spinbuttons with the current value ("Month, 7, spin button").
- Invalid submit: NVDA announces the error (role=alert) and focus moves to the first invalid control, which now reports "invalid".
- Descriptions are read after the label (aria-describedby wiring).

**Checkbox / Switch / RadioGroup / Slider / Rating / ToggleGroup / Toolbar**
- States announced: checked/unchecked/mixed, on/off, selected, pressed, value (slider reads value on every arrow press).
- Space and Enter activate as the keyboard tables state. Toolbar announces its label and is a single Tab stop.

**Tabs / Accordion / Tree / Collapsible**
- Tabs: "tab, N of M, selected"; the panel is reachable and labelled. Accordion/Collapsible: expanded/collapsed announced on toggle.
- Tree: level, position, expanded state announced; typeahead works; selection announced in multiselect.

**Toast / Sortable / Progress**
- Toasts announce on arrival without focus moving (live region); the focus hotkey (F6) reaches the viewport; Escape dismisses the focused toast.
- Sortable keyboard reorder: grab, each move, and drop are all announced (live region).
- Progress announces its value; indeterminate announces as busy/indeterminate, not a stuck number.

### VoiceOver pass (macOS)

Setup: Safari (primary; VoiceOver users predominantly use Safari), VO activated (Cmd+F5), playground running. Repeat the same per-family checklist with VO conventions: VO+Right for browse, Tab/arrows for focus mode, VO+Space to activate, rotor (VO+U) to verify landmarks, form controls, and headings enumerate correctly.

Safari-specific items to verify explicitly:
- aria-activedescendant tracking in Combobox/Autocomplete (historically weaker in Safari than Chromium).
- Focus return after Escape on overlays (WebKit is stricter about programmatic focus).
- Live-region announcements for Toast and Sortable (VoiceOver coalesces rapid updates).

### Automating SR checks later (optional)

[guidepup](https://github.com/guidepup/guidepup) can drive real NVDA (Windows) and VoiceOver (macOS) from Playwright, asserting on actual spoken output. It needs a one-time environment setup (`@guidepup/setup`) and does not run on the Linux CI runners, so it is a local/scheduled-runner layer, not part of the default suite. Candidate first targets: Dialog focus flow, Menu navigation, Combobox activedescendant announcements.

### Findings log

Record manual-pass findings in issues labelled `a11y`, one per finding, titled `a11y(<component>): <symptom>`, with the SR/browser combo and the expected vs actual announcement.
