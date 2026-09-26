import { useEffect, useState, type FormEvent } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { Plus, X } from "lucide-react";
import { manufacturersApi, vehiclesApi } from "../api/endpoints";
import { ApiError } from "../api/client";
import { ErrorBanner } from "../components/Feedback";
import type { Manufacturer, VehicleInput } from "../types";

const currentYear = new Date().getFullYear();
const ADD_NEW_VALUE = "__new__";

export function VehicleFormPage() {
  const { id } = useParams();
  const isEditing = Boolean(id);
  const navigate = useNavigate();
  const location = useLocation();

  const [manufacturers, setManufacturers] = useState<Manufacturer[]>([]);
  const [ownerName, setOwnerName] = useState("");
  const [manufacturerId, setManufacturerId] = useState<number | "">("");
  const [year, setYear] = useState("");
  const [weight, setWeight] = useState("");

  // Inline "add a new manufacturer" flow, triggered from the dropdown
  // itself — manufacturers are modeled as their own table specifically so
  // this list can grow without a code change (see the assignment's note
  // under Vehicle Manufacturers), so it's the natural place to let someone
  // add one on the spot instead of being blocked by an unlisted make.
  const [isAddingManufacturer, setIsAddingManufacturer] = useState(false);
  const [newManufacturerName, setNewManufacturerName] = useState("");
  const [addManufacturerError, setAddManufacturerError] = useState<string | null>(null);
  const [addingManufacturer, setAddingManufacturer] = useState(false);

  const [loading, setLoading] = useState(isEditing);
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);

  useEffect(() => {
    manufacturersApi.list().then(setManufacturers).catch(() => setManufacturers([]));
  }, []);

  useEffect(() => {
    if (!id) return;
    vehiclesApi
      .get(Number(id))
      .then((v) => {
        setOwnerName(v.ownerName);
        setManufacturerId(v.manufacturerId);
        setYear(String(v.yearOfManufacture));
        setWeight(String(v.weightKg));
      })
      .catch(() => setErrors(["Could not load that vehicle."]))
      .finally(() => setLoading(false));
  }, [id]);

  function handleManufacturerSelect(value: string) {
    if (value === ADD_NEW_VALUE) {
      setIsAddingManufacturer(true);
      setNewManufacturerName("");
      setAddManufacturerError(null);
    } else {
      setManufacturerId(value ? Number(value) : "");
    }
  }

  function cancelAddManufacturer() {
    setIsAddingManufacturer(false);
    setNewManufacturerName("");
    setAddManufacturerError(null);
  }

  /**
   * Both vehicle saves and inline manufacturer creation require an admin
   * session. The route itself is already gated (RequireAdmin), but that
   * only checks on mount — if the session expires while this form is open,
   * the server is still the one that actually enforces this, so a 401 here
   * sends the user to log back in (and returns them here afterwards)
   * rather than showing an error they can't do anything about.
   */
  function redirectToLoginIfUnauthorized(err: unknown): boolean {
    if (err instanceof ApiError && err.status === 401) {
      navigate("/admin/login", { state: { from: location }, replace: true });
      return true;
    }
    return false;
  }

  async function handleAddManufacturer() {
    const trimmed = newManufacturerName.trim();
    if (!trimmed) {
      setAddManufacturerError("Manufacturer name is required.");
      return;
    }

    setAddingManufacturer(true);
    setAddManufacturerError(null);
    try {
      const created = await manufacturersApi.create(trimmed);
      setManufacturers((prev) => [...prev, created].sort((a, b) => a.name.localeCompare(b.name)));
      setManufacturerId(created.id);
      setIsAddingManufacturer(false);
      setNewManufacturerName("");
    } catch (err) {
      if (redirectToLoginIfUnauthorized(err)) return;
      setAddManufacturerError(err instanceof ApiError ? err.message : "Could not add that manufacturer.");
    } finally {
      setAddingManufacturer(false);
    }
  }

  function validate(): string[] {
    const problems: string[] = [];
    if (!ownerName.trim()) problems.push("Owner's name is required.");
    if (manufacturerId === "") problems.push("Manufacturer is required.");

    const yearNum = Number(year);
    if (!year || Number.isNaN(yearNum)) {
      problems.push("Year of manufacture is required.");
    } else if (yearNum < 1886 || yearNum > currentYear + 1) {
      problems.push(`Year of manufacture must be between 1886 and ${currentYear + 1}.`);
    }

    const weightNum = Number(weight);
    if (!weight || Number.isNaN(weightNum)) {
      problems.push("Weight is required.");
    } else if (weightNum <= 0) {
      problems.push("Weight must be a positive number.");
    } else if (Math.round(weightNum * 100) / 100 !== weightNum) {
      problems.push("Weight supports at most two decimal places.");
    }

    return problems;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();

    const clientErrors = validate();
    if (clientErrors.length > 0) {
      setErrors(clientErrors);
      return;
    }

    const input: VehicleInput = {
      ownerName: ownerName.trim(),
      manufacturerId: Number(manufacturerId),
      yearOfManufacture: Number(year),
      weightKg: Number(weight),
    };

    setSaving(true);
    setErrors([]);
    try {
      if (isEditing) {
        await vehiclesApi.update(Number(id), input);
      } else {
        await vehiclesApi.create(input);
      }
      navigate("/");
    } catch (err) {
      if (redirectToLoginIfUnauthorized(err)) return;
      if (err instanceof ApiError) {
        setErrors(err.problem?.errors ?? [err.message]);
      } else {
        setErrors(["Something went wrong. Please try again."]);
      }
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return <p className="py-16 text-center text-sm text-slate">Loading vehicle&hellip;</p>;
  }

  return (
    <div className="max-w-xl space-y-6">
      <h1 className="text-xl font-semibold tracking-tight">
        {isEditing ? "Edit vehicle" : "Add a vehicle"}
      </h1>

      {errors.length > 0 && (
        <ErrorBanner message={errors.length === 1 ? errors[0] : errors.join(" ")} />
      )}

      <form onSubmit={handleSubmit} className="space-y-4 rounded-lg border border-line bg-panel p-6">
        <div>
          <label htmlFor="ownerName" className="mb-1 block text-sm font-medium">
            Owner&rsquo;s name
          </label>
          <input
            id="ownerName"
            value={ownerName}
            onChange={(e) => setOwnerName(e.target.value)}
            className="w-full rounded-md border border-line bg-white px-3 py-2 text-sm focus:border-rust"
            placeholder="Jane Turei"
          />
        </div>

        <div>
          <label htmlFor="manufacturer" className="mb-1 block text-sm font-medium">
            Manufacturer
          </label>
          <select
            id="manufacturer"
            value={isAddingManufacturer ? ADD_NEW_VALUE : manufacturerId}
            onChange={(e) => handleManufacturerSelect(e.target.value)}
            className="w-full rounded-md border border-line bg-white px-3 py-2 text-sm focus:border-rust"
          >
            <option value="">Select a manufacturer&hellip;</option>
            {manufacturers.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
            <option value={ADD_NEW_VALUE}>+ Add new manufacturer&hellip;</option>
          </select>

          {isAddingManufacturer && (
            <div className="mt-2 space-y-2 rounded-md border border-rust/30 bg-paper p-3">
              {addManufacturerError && <ErrorBanner message={addManufacturerError} />}
              <div className="flex items-center gap-2">
                <input
                  autoFocus
                  value={newManufacturerName}
                  onChange={(e) => setNewManufacturerName(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      e.preventDefault();
                      void handleAddManufacturer();
                    }
                  }}
                  placeholder="e.g. Subaru"
                  className="flex-1 rounded-md border border-line bg-white px-3 py-2 text-sm focus:border-rust"
                />
                <button
                  type="button"
                  onClick={() => void handleAddManufacturer()}
                  disabled={addingManufacturer}
                  className="flex items-center gap-1 rounded-md bg-ink px-3 py-2 text-sm font-medium text-paper hover:opacity-90 disabled:opacity-50"
                >
                  <Plus size={14} />
                  {addingManufacturer ? "Adding\u2026" : "Add"}
                </button>
                <button
                  type="button"
                  onClick={cancelAddManufacturer}
                  aria-label="Cancel adding manufacturer"
                  className="p-2 text-slate hover:text-ink"
                >
                  <X size={16} />
                </button>
              </div>
              <p className="text-xs text-slate">
                Added manufacturers are available to everyone immediately, and stay in the dropdown for future vehicles.
              </p>
            </div>
          )}
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="year" className="mb-1 block text-sm font-medium">
              Year of manufacture
            </label>
            <input
              id="year"
              inputMode="numeric"
              value={year}
              onChange={(e) => setYear(e.target.value)}
              className="w-full rounded-md border border-line bg-white px-3 py-2 text-sm font-mono focus:border-rust"
              placeholder="2019"
            />
          </div>

          <div>
            <label htmlFor="weight" className="mb-1 block text-sm font-medium">
              Weight (kg)
            </label>
            <input
              id="weight"
              inputMode="decimal"
              value={weight}
              onChange={(e) => setWeight(e.target.value)}
              className="w-full rounded-md border border-line bg-white px-3 py-2 text-sm font-mono focus:border-rust"
              placeholder="1850.75"
            />
          </div>
        </div>

        <div className="flex items-center gap-3 pt-2">
          <button
            type="submit"
            disabled={saving}
            className="rounded-md bg-ink px-4 py-2 text-sm font-medium text-paper transition-opacity hover:opacity-90 disabled:opacity-50"
          >
            {saving ? "Saving\u2026" : isEditing ? "Save changes" : "Add vehicle"}
          </button>
          <button
            type="button"
            onClick={() => navigate("/")}
            className="text-sm font-medium text-slate hover:text-ink"
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
