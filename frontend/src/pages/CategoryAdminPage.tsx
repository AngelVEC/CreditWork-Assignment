import { useEffect, useState, type FormEvent } from "react";
import { Plus, Pencil, Trash2, X } from "lucide-react";
import { categoriesApi } from "../api/endpoints";
import { ApiError } from "../api/client";
import { CategoryIcon, ICON_CHOICES } from "../components/CategoryIcon";
import { ErrorBanner } from "../components/Feedback";
import type { Category, CategoryInput } from "../types";

const emptyForm: CategoryInput = { name: "", iconKey: ICON_CHOICES[0], minWeightKg: 0, maxWeightKg: null };

export function CategoryAdminPage() {
  const [categories, setCategories] = useState<Category[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [editingId, setEditingId] = useState<number | "new" | null>(null);
  const [form, setForm] = useState<CategoryInput>(emptyForm);
  const [formErrors, setFormErrors] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);

  const [deleteError, setDeleteError] = useState<string | null>(null);

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
    setForm(emptyForm);
    setFormErrors([]);
  }

  function startEdit(category: Category) {
    setEditingId(category.id);
    setForm({
      name: category.name,
      iconKey: category.iconKey,
      minWeightKg: category.minWeightKg,
      maxWeightKg: category.maxWeightKg,
    });
    setFormErrors([]);
  }

  function cancelForm() {
    setEditingId(null);
    setFormErrors([]);
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (editingId === null) return;

    setSaving(true);
    setFormErrors([]);
    try {
      if (editingId === "new") {
        await categoriesApi.create(form);
      } else {
        await categoriesApi.update(editingId, form);
      }
      setEditingId(null);
      reload();
    } catch (err) {
      if (err instanceof ApiError) {
        setFormErrors(err.problem?.errors ?? [err.message]);
      } else {
        setFormErrors(["Something went wrong. Please try again."]);
      }
    } finally {
      setSaving(false);
    }
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

      {editingId !== null && (
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
              value={form.name}
              onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
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
                  onClick={() => setForm((f) => ({ ...f, iconKey: key }))}
                  aria-label={key}
                  aria-pressed={form.iconKey === key}
                  className={`flex h-9 w-9 items-center justify-center rounded-md border transition-colors ${
                    form.iconKey === key
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
                value={form.minWeightKg}
                onChange={(e) => setForm((f) => ({ ...f, minWeightKg: Number(e.target.value) }))}
                className="w-full rounded-md border border-line bg-white px-3 py-2 text-sm font-mono focus:border-rust"
              />
            </div>
            <div>
              <label htmlFor="maxWeight" className="mb-1 block text-sm font-medium">
                Maximum weight (kg)
              </label>
              <input
                id="maxWeight"
                inputMode="decimal"
                value={form.maxWeightKg ?? ""}
                placeholder="Unbounded"
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    maxWeightKg: e.target.value === "" ? null : Number(e.target.value),
                  }))
                }
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
