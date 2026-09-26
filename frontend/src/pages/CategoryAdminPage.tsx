import { useEffect, useState, type FormEvent } from "react";
import { Plus, Pencil, Trash2, X, ArrowRight, Check } from "lucide-react";
import { categoriesApi } from "../api/endpoints";
import { ApiError } from "../api/client";
import { CategoryIcon, ICON_CHOICES } from "../components/CategoryIcon";
import { ErrorBanner } from "../components/Feedback";
import type { Category, CategoryInput } from "../types";

/** A neighboring category's boundary that will move automatically to stay adjacent. */
interface CascadeChange {
  neighborName: string;
  field: "minimum" | "maximum";
  oldValue: number | null;
  newValue: number | null;
}

function formatKg(value: number | null): string {
  return value === null ? "unbounded" : `${value.toFixed(2)}kg`;
}

/**
 * Mirrors the server's cascade logic (CategoryService.UpdateAsync) so the
 * user sees, before saving, exactly which neighboring category will also
 * change — rather than that change happening silently.
 */
function computeCascade(
  categories: Category[],
  editingId: number,
  original: Category,
  newMin: number,
  newMax: number | null
): CascadeChange[] {
  const changes: CascadeChange[] = [];

  if (newMin !== original.minWeightKg) {
    const neighbor = categories.find((c) => c.id !== editingId && c.maxWeightKg === original.minWeightKg);
    if (neighbor) {
      changes.push({ neighborName: neighbor.name, field: "maximum", oldValue: neighbor.maxWeightKg, newValue: newMin });
    }
  }

  if (newMax !== original.maxWeightKg) {
    const neighbor = categories.find((c) => c.id !== editingId && c.minWeightKg === original.maxWeightKg);
    if (neighbor) {
      changes.push({ neighborName: neighbor.name, field: "minimum", oldValue: neighbor.minWeightKg, newValue: newMax });
    }
  }

  return changes;
}

export function CategoryAdminPage() {
  const [categories, setCategories] = useState<Category[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [editingId, setEditingId] = useState<number | "new" | null>(null);
  const [originalCategory, setOriginalCategory] = useState<Category | null>(null);

  // Weight fields are kept as free-typed strings, not numbers, so a
  // mid-edit state like "-" or "" or "12." never gets coerced into NaN and
  // "stuck" in the box — they're only parsed to numbers on submit.
  const [name, setName] = useState("");
  const [iconKey, setIconKey] = useState(ICON_CHOICES[0]);
  const [minWeightInput, setMinWeightInput] = useState("0");
  const [maxWeightInput, setMaxWeightInput] = useState("");

  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);

  const [deleteError, setDeleteError] = useState<string | null>(null);

  // Set only when a save would also move a neighboring category's boundary
  // — the confirmation panel is shown instead of saving immediately.
  const [pendingCascade, setPendingCascade] = useState<CascadeChange[] | null>(null);
  const [pendingValues, setPendingValues] = useState<{ minWeightKg: number; maxWeightKg: number | null } | null>(
    null
  );

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

  function resetFormState() {
    setFormErrors([]);
    setPendingCascade(null);
    setPendingValues(null);
  }

  function startCreate() {
    setEditingId("new");
    setOriginalCategory(null);
    setName("");
    setIconKey(ICON_CHOICES[0]);
    setMinWeightInput("0");
    setMaxWeightInput("");
    resetFormState();
  }

  function startEdit(category: Category) {
    setEditingId(category.id);
    setOriginalCategory(category);
    setName(category.name);
    setIconKey(category.iconKey);
    setMinWeightInput(String(category.minWeightKg));
    setMaxWeightInput(category.maxWeightKg === null ? "" : String(category.maxWeightKg));
    resetFormState();
  }

  function cancelForm() {
    setEditingId(null);
    setOriginalCategory(null);
    resetFormState();
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
      resetFormState();
      reload();
    } catch (err) {
      setPendingCascade(null);
      setPendingValues(null);
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
    if (!input) return;

    // Editing an existing category whose boundary touches a neighbor:
    // show what will also change instead of saving immediately.
    if (editingId !== "new" && originalCategory && categories) {
      const cascade = computeCascade(categories, editingId, originalCategory, input.minWeightKg, input.maxWeightKg);
      if (cascade.length > 0) {
        setFormErrors([]);
        setPendingCascade(cascade);
        setPendingValues({ minWeightKg: input.minWeightKg, maxWeightKg: input.maxWeightKg });
        return;
      }
    }

    await saveCategory(input);
  }

  async function confirmCascadeAndSave() {
    if (!pendingValues) return;
    await saveCategory({ name: name.trim(), iconKey, minWeightKg: pendingValues.minWeightKg, maxWeightKg: pendingValues.maxWeightKg });
  }

  async function handleDelete(category: Category) {
    if (!window.confirm(`Delete category "${category.name}"? This cannot be undone.`)) return;

    setDeleteError(null);
    try {
      await categoriesApi.remove(category.id);
      reload();
    } catch (err) {
      setDeleteError(
        err instanceof ApiError ? err.message : `Could not delete "${category.name}".`
      );
    }
  }

  const showingCascadeConfirmation = pendingCascade !== null && pendingCascade.length > 0;

  return (
    <div className="max-w-2xl space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Vehicle categories</h1>
          <p className="mt-1 text-sm text-slate">
            Ranges must cover every weight from 0kg upward with no gaps or overlaps.
          </p>
        </div>
        {editingId === null && (
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
      {deleteError && <ErrorBanner message={deleteError} />}

      {editingId !== null && showingCascadeConfirmation && (
        <div className="space-y-4 rounded-lg border border-rust/30 bg-panel p-6">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-semibold">Confirm boundary change</h2>
            <button
              type="button"
              onClick={() => {
                setPendingCascade(null);
                setPendingValues(null);
              }}
              className="text-slate hover:text-ink"
              aria-label="Back to editing"
            >
              <X size={18} />
            </button>
          </div>

          <p className="text-sm text-slate">
            Since category ranges can&rsquo;t have gaps or overlaps, this will also move the
            neighboring {pendingCascade!.length === 1 ? "category" : "categories"} that shares this boundary:
          </p>

          <ul className="space-y-2">
            {pendingCascade!.map((change, i) => (
              <li
                key={i}
                className="flex items-center justify-between rounded-md border border-line bg-paper px-3 py-2 text-sm"
              >
                <span>
                  <span className="font-medium">{change.neighborName}</span>&rsquo;s {change.field} weight
                </span>
                <span className="flex items-center gap-1.5 font-mono text-xs">
                  {formatKg(change.oldValue)}
                  <ArrowRight size={12} className="text-slate" />
                  <span className="font-semibold text-rust-dark">{formatKg(change.newValue)}</span>
                </span>
              </li>
            ))}
          </ul>

          <div className="flex items-center gap-3 pt-2">
            <button
              type="button"
              onClick={() => void confirmCascadeAndSave()}
              disabled={saving}
              className="flex items-center gap-1.5 rounded-md bg-ink px-4 py-2 text-sm font-medium text-paper hover:opacity-90 disabled:opacity-50"
            >
              <Check size={16} />
              {saving ? "Saving\u2026" : "Confirm & save"}
            </button>
            <button
              type="button"
              onClick={() => {
                setPendingCascade(null);
                setPendingValues(null);
              }}
              className="text-sm font-medium text-slate hover:text-ink"
            >
              Back to editing
            </button>
          </div>
        </div>
      )}

      {editingId !== null && !showingCascadeConfirmation && (
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
                  <p className="font-mono text-xs text-slate">
                    {c.minWeightKg.toFixed(2)}kg &ndash; {c.maxWeightKg === null ? "and above" : `${c.maxWeightKg.toFixed(2)}kg`}
                  </p>
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
                  onClick={() => void handleDelete(c)}
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
