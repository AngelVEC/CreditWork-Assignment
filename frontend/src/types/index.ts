export interface Vehicle {
  id: number;
  ownerName: string;
  manufacturerId: number;
  manufacturerName: string;
  yearOfManufacture: number;
  weightKg: number;
  categoryName: string;
  categoryIconKey: string;
}

export interface VehicleInput {
  ownerName: string;
  manufacturerId: number;
  yearOfManufacture: number;
  weightKg: number;
}

export interface Manufacturer {
  id: number;
  name: string;
}

export interface Category {
  id: number;
  name: string;
  iconKey: string;
  minWeightKg: number;
  maxWeightKg: number | null;
}

export interface CategoryInput {
  name: string;
  iconKey: string;
  minWeightKg: number;
  maxWeightKg: number | null;
}

export type SortBy = "ownerName" | "manufacturer" | "year" | "weight";
export type SortDir = "asc" | "desc";

export interface Session {
  username: string;
  role: string;
  expiresAtUtc: string;
}

/** RFC 7807 ProblemDetails, as returned by the API's error middleware. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: string[];
}
