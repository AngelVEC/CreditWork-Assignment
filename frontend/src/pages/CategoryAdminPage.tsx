import { useEffect, useState, type FormEvent } from "react";
import { Plus, Pencil, Trash2, X, ArrowRight, Check, Split } from "lucide-react";
import { categoriesApi } from "../api/endpoints";
import { ApiError } from "../api/client";
import { CategoryIcon, ICON_CHOICES } from "../components/CategoryIcon";
import { ErrorBanner } from "../components/Feedback";
import type { Category, CategoryInput } from "../types";

/** A neighboring category's boundary that will move automatically to stay adjacent. */
interface BoundaryChange {
  kind: "boundary";
  neighborName: string;
  field: "minimum" | "maximum";
  oldValue: number | null;
  newValue: number | null;
}

/** An existing category that will be split in two around the new category. */
interface SplitChange {
  kind: "split";
  categoryName: string;
  lowerMin: number;
  lowerMax: number;
  upperRemainderName: string;
  upperMin: number;
  upperMax: number | null;
}

type ConfirmationChange = BoundaryChange | SplitChange;

/** A save/delete action waiting on the user to confirm its side effects. */
interface PendingConfirmation {
  title: string;
  description: string;
  changes: ConfirmationChange[];
  confirmLabel: string;
  danger?: boolean;
  run: () => Promise<void>;
}

function formatKg(value: number | null): string {
  return value === null ? "unbounded" : `${value.toFixed(2)}kg`;
}

function formatRange(min: number, max: number | null): string {
  return `${min.toFixed(2)}kg \u2013 ${max === null ? "and above" : `${max.toFixed(2)}kg`}`;
}

// --- Cascade prediction, mirroring the server's logic in CategoryService --
// so the confirmation panel shows exactly what will happen before it does.

/** Edit: a category's boundary moved — find the neighbor that shared it. */
function computeEditCascade(
  categories: Category[],
  editingId: number,
  original: Category,
  newMin: number,
  newMax: number | null
): BoundaryChange[] {
  const changes: BoundaryChange[] = [];

  if (newMin !== original.minWeightKg) {
    const neighbor = categories.find((c) => c.id !== editingId && c.maxWeightKg === original.minWeightKg);
    if (neighbor) {
      changes.push({ kind: "boundary", neighborName: neighbor.name, field: "maximum", oldValue: neighbor.maxWeightKg, newValue: newMin });
    }
  }

  if (newMax !== original.maxWeightKg) {
    const neighbor = categories.find((c) => c.id !== editingId && c.minWeightKg === original.maxWeightKg);
    if (neighbor) {
      changes.push({ kind: "boundary", neighborName: neighbor.name, field: "minimum", oldValue: neighbor.minWeightKg, newValue: newMax });
    }
  }

  return changes;
}

function generateRemainderName(baseName: string, takenNames: string[]): string {
  const taken = new Set(takenNames.map((n) => n.toLowerCase()));
  let counter = 2;
  let candidate = `${baseName} (${counter})`;
  while (taken.has(candidate.toLowerCase())) {
    counter++;
    candidate = `${baseName} (${counter})`;
  }
  return candidate;
}

/**
 * Create: classifies how the new range interacts with each existing
 * category — touches its bottom edge, reaches past its top edge, both
 * (full engulf, unsupported), or neither (strictly inside -> three-way
 * split). Mirrors CategoryService.CreateAsync exactly.
 */
function computeCreateCascade(categories: Category[], newName: string, newMin: number, newMax: number | null): ConfirmationChange[] {
  const changes: ConfirmationChange[] = [];
  const takenNames = categories.map((c) => c.name).concat(newName);

  for (const other of categories) {
    const otherReachesPastNewMin = other.maxWeightKg === null || other.maxWeightKg > newMin;
    const newReachesPastOtherMin = newMax === null || newMax > other.minWeightKg;
    const overlaps = otherReachesPastNewMin && newReachesPastOtherMin;
    if (!overlaps) continue;

    const touchesBottom = newMin <= other.minWeightKg;
    const reachesTop = newMax === null || (other.maxWeightKg !== null && newMax >= other.maxWeightKg);

    if (touchesBottom && reachesTop) {
      continue; // full engulf — unsupported, will fail validation with a clear error
    }

    if (touchesBottom) {
      changes.push({ kind: "boundary", neighborName: other.name, field: "minimum", oldValue: other.minWeightKg, newValue: newMax });
    } else if (reachesTop) {
      changes.push({ kind: "boundary", neighborName: other.name, field: "maximum", oldValue: other.maxWeightKg, newValue: newMin });
    } else {
      const remainderName = generateRemainderName(other.name, takenNames);
      changes.push({
        kind: "split",
        categoryName: other.name,
        lowerMin: other.minWeightKg,
        lowerMax: newMin,
        upperRemainderName: remainderName,
        upperMin: newMax as number,
        upperMax: other.maxWeightKg,
      });
    }
  }

  return changes;
}

/** Delete: which neighbor will absorb the deleted range (lower neighbor preferred)? */
function computeDeleteAbsorption(categories: Category[], deleted: Category): BoundaryChange | null {
  const others = categories.filter((c) => c.id !== deleted.id);

  const lowerNeighbor = others.find((c) => c.maxWeightKg === deleted.minWeightKg);
  if (lowerNeighbor) {
    return { kind: "boundary", neighborName: lowerNeighbor.name, field: "maximum", oldValue: lowerNeighbor.maxWeightKg, newValue: deleted.maxWeightKg };
  }

  const upperNeighbor = others.find((c) => c.minWeightKg === deleted.maxWeightKg);
  if (upperNeighbor) {
    return { kind: "boundary", neighborName: upperNeighbor.name, field: "minimum", oldValue: upperNeighbor.minWeightKg, newValue: deleted.minWeightKg };
  }

  return null;
}

const defaultIconKey = ICON_CHOICES[0];

export function CategoryAdminPage() {
  const [categories, setCategories] = useState<Category[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [editingId, setEditingId] = useState<number | "new" | null>(null);
  const [originalCategory, setOriginalCategory] = useState<Category | null>(null);

  // Weight fields are kept as free-typed strings, not numbers, so a
  // mid-edit state like "-" or "" or "12." never gets coerced into NaN and
  // "stuck" in the box — they're only parsed to numbers on submit.
  const [name, setName] = useState("");
  const [iconKey, setIconKey] = useState(defaultIconKey);
  const [minWeightInput, setMinWeightInput] = useState("0");
  const [maxWeightInput, setMaxWeightInput] = useState("");

  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);

  const [actionError, setActionError] = useState<string | null>(null);

  // Set whenever a save or delete would have a side effect the user should
  // see and approve first: a neighboring category's boundary moving, an
  // existing category being split in two, or a deleted range being
  // absorbed into a neighbor.
  const [pending, setPending] = useState<PendingConfirmation | null>(null);

  function reload() {
    categoriesApi
      .list()
      .then((data) => {
        setCategories(data.slice().sort((a, b) => a.minWeightKg - b.minWeightKg));
        setLoadError(null);
      })
      .catch((err) => setLoadError(err instanceof ApiError ? err.message : "Could not load categories."));
  }

  useEffect(reload, []);

  function startCreate() {
    setEditingId("new");
    setOriginalCategory(null);
    setName("");
    setIconKey(defaultIconKey);
    setMinWeightInput("0");
    setMaxWeightInput("");
    setFormErrors([]);
    setPending(null);
  }

  function startEdit(category: Category) {
    setEditingId(category.id);
    setOriginalCategory(category);
    setName(category.name);
    setIconKey(category.iconKey);
    setMinWeightInput(String(category.minWeightKg));
    setMaxWeightInput(category.maxWeightKg === null ? "" : String(category.maxWeightKg));
    setFormErrors([]);
    setPending(null);
  }

  function cancelForm() {
    setEditingId(null);
    setOriginalCategory(null);
    setFormErrors([]);
    setPending(null);
  }

  /** Validates + parses the free-typed weight strings. Returns null (with formErrors set) if invalid. */
  function parseForm(): CategoryInput | null {
    const errors: string[] = [];

    if (!name.trim()) errors.push("Category name is required.");

    let minWeightKg = NaN;
    if (minWeightInput.trim() === "") {
      errors.push("Minimum weight is required.");
    } else {
      minWeightKg = Number(minWeightInput);
      if (Number.isNaN(minWeightKg)) errors.push("Minimum weight must be a valid number.");
    }

    let maxWeightKg: number | null = null;
    if (maxWeightInput.trim() !== "") {
      maxWeightKg = Number(maxWeightInput);
      if (Number.isNaN(maxWeightKg)) {
        errors.push("Maximum weight must be a valid number, or left blank for the unbounded top category.");
      }
    }

    if (errors.length > 0) {
      setFormErrors(errors);
      return null;
    }

    return { name: name.trim(), iconKey, minWeightKg, maxWeightKg };
  }

  async function saveCategory(input: CategoryInput) {
    setSaving(true);
    setFormErrors([]);
    try {
      if (editingId === "new") {
        await categoriesApi.create(input);
      } else if (editingId !== null) {
        await categoriesApi.update(editingId, input);
      }
      setEditingId(null);
      setOriginalCategory(null);
      setPending(null);
      reload();
    } catch (err) {
      setPending(null);
      if (err instanceof ApiError) {
        setFormErrors(err.problem?.errors ?? [err.message]);
      } else {
        setFormErrors(["Something went wrong. Please try again."]);
      }
    } finally {
      setSaving(false);
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (editingId === null) return;

    const input = parseForm();
    if (!input || !categories) return;

    if (editingId === "new") {
      const changes = computeCreateCascade(categories, input.name, input.minWeightKg, input.maxWeightKg);
      if (changes.length > 0) {
        setPending({
          title: "Confirm new category",
          description: describeChanges("add", input.name, changes),
          changes,
          confirmLabel: "Confirm & add",
          run: () => saveCategory(input),
        });
        return;
      }
    } else if (originalCategory) {
      const changes = computeEditCascade(categories, editingId, originalCategory, input.minWeightKg, input.maxWeightKg);
      if (changes.length > 0) {
        setPending({
          title: "Confirm boundary change",
          description: describeChanges("edit", input.name, changes),
          changes,
          confirmLabel: "Confirm & save",
          run: () => saveCategory(input),
        });
        return;
      }
    }

    await saveCategory(input);
  }

  function describeChanges(kind: "add" | "edit", categoryName: string, changes: ConfirmationChange[]): string {
    const hasSplit = changes.some((c) => c.kind === "split");
    const verb = kind === "add" ? "Adding" : "Saving";

    if (hasSplit) {
      return `${verb} "${categoryName}" lands inside an existing category's range, so it will be split into two around it. Here's exactly what will change:`;
    }

    const plural = changes.length > 1;
    return kind === "add"
      ? `Adding "${categoryName}" will also adjust the ${plural ? "neighboring categories" : "neighboring category"} that share${plural ? "" : "s"} its boundaries:`
      : `Since category ranges can't have gaps or overlaps, this will also move the ${plural ? "neighboring categories" : "neighboring category"} that share${plural ? "" : "s"} this boundary:`;
  }

  async function performDelete(category: Category) {
    setActionError(null);
    try {
      await categoriesApi.remove(category.id);
      setPending(null);
      reload();
    } catch (err) {
      setPending(null);
      setActionError(err instanceof ApiError ? err.message : `Could not delete "${category.name}".`);
    }
  }

  function requestDelete(category: Category) {
    setEditingId(null);
    setActionError(null);

    const absorption = categories ? computeDeleteAbsorption(categories, category) : null;

    setPending({
      title: "Confirm delete",
      description: absorption
        ? `Deleting "${category.name}" will extend "${absorption.neighborName}" to cover its range, so every weight still has a category:`
        : `Delete category "${category.name}"? This cannot be undone.`,
      changes: absorption ? [absorption] : [],
      confirmLabel: "Delete category",
      danger: true,
      run: () => performDelete(category),
    });
  }

  return (
    <div className="max-w-2xl space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Vehicle categories</h1>
          <p className="mt-1 text-sm text-slate">
            Ranges must cover every weight from 0kg upward with no gaps or overlaps.
          </p>
        </div>
        {editingId === null && !pending && (
          <button
            onClick={startCreate}
            className="flex items-center gap-1.5 rounded-md bg-ink px-3 py-2 text-sm font-medium text-paper hover:opacity-90"
          >
            <Plus size={16} />
            New category
          </button>
        )}
      </div>

      {loadError && <ErrorBanner message={loadError} />}
      {actionError && <ErrorBanner message={actionError} />}

      {pending && (
        <div className={`space-y-4 rounded-lg border bg-panel p-6 ${pending.danger ? "border-rust-dark/40" : "border-rust/30"}`}>
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold">{pending.title}</h2>
            <button
              type="button"
              onClick={() => setPending(null)}
              className="text-slate hover:text-ink"
              aria-label="Cancel"
            >
              <X size={18} />
            </button>
          </div>

          <p className="text-sm text-slate">{pending.description}</p>

          {pending.changes.length > 0 && (
            <ul className="space-y-2">
              {pending.changes.map((change, i) => (
                <li key={i} className="rounded-md border border-line bg-paper px-3 py-2 text-sm">
                  {change.kind === "boundary" ? (
                    <div className="flex items-center justify-between">
                      <span>
                        <span className="font-medium">{change.neighborName}</span>&rsquo;s {change.field} weight
                      </span>
                      <span className="flex items-center gap-1.5 font-mono text-xs">
                        {formatKg(change.oldValue)}
                        <ArrowRight size={12} className="text-slate" />
                        <span className="font-semibold text-rust-dark">{formatKg(change.newValue)}</span>
                      </span>
                    </div>
                  ) : (
                    <div className="space-y-1.5">
                      <div className="flex items-center gap-1.5 font-medium">
                        <Split size={14} className="text-rust" />
                        <span>{change.categoryName}</span> will be split into two categories
                      </div>
                      <ul className="ml-5 space-y-1 font-mono text-xs text-slate">
                        <li>
                          <span className="font-semibold text-ink">{change.categoryName}</span> keeps{" "}
                          {formatRange(change.lowerMin, change.lowerMax)}
                        </li>
                        <li>
                          <span className="font-semibold text-rust-dark">{change.upperRemainderName}</span>{" "}
                          <span className="rounded bg-rust/10 px-1 py-0.5 text-[10px] font-sans font-medium uppercase tracking-wide text-rust-dark">
                            new
                          </span>{" "}
                          covers {formatRange(change.upperMin, change.upperMax)}
                        </li>
                      </ul>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}

          <div className="flex items-center gap-3 pt-2">
            <button
              type="button"
              onClick={() => void pending.run()}
              disabled={saving}
              className={`flex items-center gap-1.5 rounded-md px-4 py-2 text-sm font-medium text-paper hover:opacity-90 disabled:opacity-50 ${
                pending.danger ? "bg-rust-dark" : "bg-ink"
              }`}
            >
              {pending.danger ? <Trash2 size={16} /> : <Check size={16} />}
              {saving ? "Working\u2026" : pending.confirmLabel}
            </button>
            <button type="button" onClick={() => setPending(null)} className="text-sm font-medium text-slate hover:text-ink">
              {editingId !== null ? "Back to editing" : "Cancel"}
            </button>
          </div>
        </div>
      )}

      {editingId !== null && !pending && (
        <form onSubmit={handleSubmit} className="space-y-4 rounded-lg border border-rust/30 bg-panel p-6">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold">{editingId === "new" ? "New category" : "Edit category"}</h2>
            <button type="button" onClick={cancelForm} className="text-slate hover:text-ink" aria-label="Cancel">
              <X size={18} />
            </button>
          </div>

          {formErrors.length > 0 && <ErrorBanner message={formErrors.join(" ")} />}

          <div>
            <label htmlFor="catName" className="mb-1 block text-sm font-medium">
              Name
            </label>
            <input
              id="catName"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full rounded-md border border-line bg-white px-3 py-2 text-sm focus:border-rust"
              placeholder="Medium"
            />
          </div>

          <div>
            <span className="mb-1 block text-sm font-medium">Icon</span>
            <div className="flex flex-wrap gap-2">
              {ICON_CHOICES.map((key) => (
                <button
                  type="button"
                  key={key}
                  onClick={() => setIconKey(key)}
                  aria-label={key}
                  aria-pressed={iconKey === key}
                  className={`flex h-9 w-9 items-center justify-center rounded-md border transition-colors ${
                    iconKey === key
                      ? "border-rust bg-rust/10 text-rust"
                      : "border-line text-slate hover:border-slate"
                  }`}
                >
                  <CategoryIcon iconKey={key} className="h-4 w-4" />
                </button>
              ))}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label htmlFor="minWeight" className="mb-1 block text-sm font-medium">
                Minimum weight (kg)
              </label>
              <input
                id="minWeight"
                inputMode="decimal"
                value={minWeightInput}
                onChange={(e) => setMinWeightInput(e.target.value)}
                className="w-full rounded-md border border-line bg-white px-3 py-2 text-sm font-mono focus:border-rust"
                placeholder="0"
              />
            </div>
            <div>
              <label htmlFor="maxWeight" className="mb-1 block text-sm font-medium">
                Maximum weight (kg)
              </label>
              <input
                id="maxWeight"
                inputMode="decimal"
                value={maxWeightInput}
                placeholder="Unbounded"
                onChange={(e) => setMaxWeightInput(e.target.value)}
                className="w-full rounded-md border border-line bg-white px-3 py-2 text-sm font-mono focus:border-rust"
              />
              <p className="mt-1 text-xs text-slate">Leave blank for the top, unbounded category.</p>
            </div>
          </div>

          <div className="flex items-center gap-3 pt-2">
            <button
              type="submit"
              disabled={saving}
              className="rounded-md bg-ink px-4 py-2 text-sm font-medium text-paper hover:opacity-90 disabled:opacity-50"
            >
              {saving ? "Saving\u2026" : "Save category"}
            </button>
            <button type="button" onClick={cancelForm} className="text-sm font-medium text-slate hover:text-ink">
              Cancel
            </button>
          </div>
        </form>
      )}

      {categories && (
        <ul className="divide-y divide-line rounded-lg border border-line bg-panel">
          {categories.map((c) => (
            <li key={c.id} className="flex items-center justify-between gap-4 px-4 py-3">
              <div className="flex items-center gap-3">
                <span className="flex h-9 w-9 items-center justify-center rounded-md bg-paper text-rust">
                  <CategoryIcon iconKey={c.iconKey} className="h-4 w-4" />
                </span>
                <div>
                  <p className="text-sm font-medium">{c.name}</p>
                  <p className="font-mono text-xs text-slate">{formatRange(c.minWeightKg, c.maxWeightKg)}</p>
                </div>
              </div>
              <div className="flex items-center gap-3">
                <button
                  onClick={() => startEdit(c)}
                  className="flex items-center gap-1 text-xs font-medium text-slate hover:text-rust"
                >
                  <Pencil size={14} />
                  Edit
                </button>
                <button
                  onClick={() => requestDelete(c)}
                  className="flex items-center gap-1 text-xs font-medium text-slate hover:text-rust-dark"
                >
                  <Trash2 size={14} />
                  Delete
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
