import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ArrowUp, ArrowDown, ArrowUpDown, Pencil } from "lucide-react";
import { vehiclesApi } from "../api/endpoints";
import { ApiError } from "../api/client";
import { CategoryIcon } from "../components/CategoryIcon";
import { ErrorBanner, EmptyState } from "../components/Feedback";
import type { SortBy, SortDir, Vehicle } from "../types";

const COLUMNS: { key: SortBy; label: string }[] = [
  { key: "ownerName", label: "Owner" },
  { key: "manufacturer", label: "Manufacturer" },
  { key: "year", label: "Year" },
  { key: "weight", label: "Weight (kg)" },
];

export function VehicleListPage() {
  const [vehicles, setVehicles] = useState<Vehicle[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sortBy, setSortBy] = useState<SortBy>("ownerName");
  const [sortDir, setSortDir] = useState<SortDir>("asc");

  useEffect(() => {
    let cancelled = false;
    setError(null);

    vehiclesApi
      .list(sortBy, sortDir)
      .then((data) => {
        if (!cancelled) setVehicles(data);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err instanceof ApiError ? err.message : "Could not load vehicles.");
      });

    return () => {
      cancelled = true;
    };
  }, [sortBy, sortDir]);

  function handleSort(column: SortBy) {
    if (column === sortBy) {
      setSortDir((dir) => (dir === "asc" ? "desc" : "asc"));
    } else {
      setSortBy(column);
      setSortDir("asc");
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold tracking-tight">Vehicle Register</h1>
        <p className="text-sm text-slate">
          {vehicles ? `${vehicles.length} vehicle${vehicles.length === 1 ? "" : "s"}` : "Loading\u2026"}
        </p>
      </div>

      {error && <ErrorBanner message={error} />}

      {vehicles && vehicles.length === 0 && !error && (
        <EmptyState message="No vehicles yet. Add the first one to get started." />
      )}

      {vehicles && vehicles.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-line bg-panel">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead>
              <tr className="border-b border-line text-xs uppercase tracking-wide text-slate">
                {COLUMNS.map((col) => (
                  <th key={col.key} className="px-4 py-3 font-medium">
                    <button
                      onClick={() => handleSort(col.key)}
                      className="flex items-center gap-1 hover:text-ink"
                      aria-label={`Sort by ${col.label}`}
                    >
                      {col.label}
                      {sortBy === col.key ? (
                        sortDir === "asc" ? (
                          <ArrowUp size={14} />
                        ) : (
                          <ArrowDown size={14} />
                        )
                      ) : (
                        <ArrowUpDown size={14} className="opacity-30" />
                      )}
                    </button>
                  </th>
                ))}
                <th className="px-4 py-3 font-medium">Category</th>
                <th className="px-4 py-3 font-medium sr-only">Edit</th>
              </tr>
            </thead>
            <tbody>
              {vehicles.map((v) => (
                <tr key={v.id} className="border-b border-line last:border-0 hover:bg-paper/60">
                  <td className="px-4 py-3">{v.ownerName}</td>
                  <td className="px-4 py-3">{v.manufacturerName}</td>
                  <td className="px-4 py-3 font-mono">{v.yearOfManufacture}</td>
                  <td className="px-4 py-3 font-mono">{v.weightKg.toFixed(2)}</td>
                  <td className="px-4 py-3">
                    <span className="inline-flex items-center gap-1.5 rounded-full bg-paper px-2.5 py-1 text-xs font-medium">
                      <CategoryIcon iconKey={v.categoryIconKey} className="h-3.5 w-3.5 text-rust" />
                      {v.categoryName}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <Link
                      to={`/vehicles/${v.id}/edit`}
                      className="inline-flex items-center gap-1 text-xs font-medium text-slate hover:text-rust"
                      aria-label={`Edit ${v.ownerName}'s vehicle`}
                    >
                      <Pencil size={14} />
                      Edit
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
