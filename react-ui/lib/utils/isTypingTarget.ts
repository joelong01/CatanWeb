/**
 * Shared input-target guard for keyboard shortcut handlers.
 *
 * Returns true when the event target is "typing" — a context where
 * keystrokes are part of the user's text entry and game shortcuts must
 * not fire. Returns false for non-text inputs (checkbox, radio, range,
 * ...) and for readonly/disabled inputs where the browser ignores
 * typing anyway; in those cases shortcuts SHOULD fire.
 *
 * Contract:
 * - contentEditable element → typing
 * - <textarea> (not readonly/disabled) → typing
 * - <input> with a text-like type (not readonly/disabled) → typing
 * - <select> (not disabled) → typing (type-ahead conflicts otherwise)
 * - Anything else (including <button>, focusable <div>, etc.) → not typing
 *
 * See .design/keyboard-shortcuts.md for the full rationale.
 */

const NON_TEXT_INPUT_TYPES: ReadonlySet<string> = new Set([
  'checkbox',
  'radio',
  'button',
  'submit',
  'reset',
  'image',
  'range',
  'color',
  'file',
]);

export function isTypingTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable) return true;

  if (target instanceof HTMLTextAreaElement) {
    return !target.readOnly && !target.disabled;
  }

  if (target instanceof HTMLInputElement) {
    if (target.readOnly || target.disabled) return false;
    return !NON_TEXT_INPUT_TYPES.has(target.type);
  }

  if (target instanceof HTMLSelectElement) {
    return !target.disabled;
  }

  return false;
}
