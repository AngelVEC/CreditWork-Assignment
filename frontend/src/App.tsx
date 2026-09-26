import { Route, Routes } from "react-router-dom";
import { Navbar } from "./components/Navbar";
import { RequireAdmin } from "./components/RequireAdmin";
import { VehicleListPage } from "./pages/VehicleListPage";
import { VehicleFormPage } from "./pages/VehicleFormPage";
import { CategoryAdminPage } from "./pages/CategoryAdminPage";
import { LoginPage } from "./pages/LoginPage";

export default function App() {
  return (
    <div className="min-h-screen">
      <Navbar />
      <main className="mx-auto max-w-5xl px-4 py-8">
        <Routes>
          <Route path="/" element={<VehicleListPage />} />
          <Route
            path="/vehicles/new"
            element={
              <RequireAdmin>
                <VehicleFormPage />
              </RequireAdmin>
            }
          />
          <Route
            path="/vehicles/:id/edit"
            element={
              <RequireAdmin>
                <VehicleFormPage />
              </RequireAdmin>
            }
          />
          <Route path="/admin/login" element={<LoginPage />} />
          <Route
            path="/admin/categories"
            element={
              <RequireAdmin>
                <CategoryAdminPage />
              </RequireAdmin>
            }
          />
        </Routes>
      </main>
    </div>
  );
}
