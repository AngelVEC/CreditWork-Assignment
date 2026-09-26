import { AlertCircle, Inbox } from "lucide-react";

export function ErrorBanner({ message }: { message: string }) {
  return (
    <div className="flex items-start gap-2 rounded-md border border-rust/30 bg-rust/5 px-4 py-3 text-sm text-rust-dark">
      <AlertCircle size={18} className="mt-0.5 flex-shrink-0" />
      <span>{message}</span>
    </div>
  );
}

export function EmptyState({ message }: { message: string }) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-lg border border-dashed border-line py-16 text-center text-slate">
      <Inbox size={28} className="text-line" />
      <p className="text-sm">{message}</p>
    </div>
  );
}
