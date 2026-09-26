import { api } from "./client";
import type {
  Category,
  CategoryInput,
  Manufacturer,
  Session,
  SortBy,
  SortDir,
  Vehicle,
  VehicleInput,
} from "../types";

export const vehiclesApi = {
  list: (sortBy: SortBy, sortDir: SortDir) =>
    api.get<Vehicle[]>(`/api/vehicles?sortBy=${sortBy}&sortDir=${sortDir}`),
  get: (id: number) => api.get<Vehicle>(`/api/vehicles/${id}`),
  create: (input: VehicleInput) => api.post<Vehicle>("/api/vehicles", input),
  update: (id: number, input: VehicleInput) => api.put<Vehicle>(`/api/vehicles/${id}`, input),
  remove: (id: number) => api.delete<void>(`/api/vehicles/${id}`),
};

export const manufacturersApi = {
  list: () => api.get<Manufacturer[]>("/api/manufacturers"),
};

export const categoriesApi = {
  list: () => api.get<Category[]>("/api/categories"),
  create: (input: CategoryInput) => api.post<Category>("/api/categories", input),
  update: (id: number, input: CategoryInput) => api.put<Category>(`/api/categories/${id}`, input),
  remove: (id: number) => api.delete<void>(`/api/categories/${id}`),
};

export const authApi = {
  login: (username: string, password: string) =>
    api.post<Session>("/api/auth/login", { username, password }),
  session: () => api.get<Session>("/api/auth/session"),
  logout: () => api.post<void>("/api/auth/logout"),
};
