"use client";

interface DeleteButtonProps {
  onClick: () => void;
  loading?: boolean;
  label?: string;
  className?: string;
}

export function DeleteButton({
  onClick,
  loading = false,
  label = "Delete",
  className = "",
}: DeleteButtonProps) {
  return (
    <button
      onClick={onClick}
      disabled={loading}
      className={`flex items-center gap-2 px-4 py-2 bg-red-600 text-white text-sm font-medium rounded-md hover:bg-red-700 disabled:opacity-50 ${className}`}
    >
      {loading ? (
        <>
          <span className="inline-block w-3 h-3 border-2 border-white border-t-transparent rounded-full animate-spin" />
          Deleting...
        </>
      ) : (
        label
      )}
    </button>
  );
}
