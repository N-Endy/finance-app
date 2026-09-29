"use client";

import { useOfflineQueue } from "@/lib/offline-queue";

export function OfflineIndicator() {
  const { pendingCount, isSyncing, isOnline, syncNow } = useOfflineQueue();

  if (isOnline && pendingCount === 0) return null;

  return (
    <div style={{
      display: "flex",
      alignItems: "center",
      justifyContent: "space-between",
      padding: "8px 16px",
      borderRadius: 8,
      backgroundColor: !isOnline ? "rgba(240, 180, 41, 0.12)" : "rgba(30, 224, 135, 0.12)",
      border: `1px solid ${!isOnline ? "#f0b429" : "#1ee087"}`,
      color: !isOnline ? "#f0b429" : "#1ee087",
      fontSize: "0.85rem",
      marginBottom: 16
    }}>
      <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
        <span style={{
          width: 8,
          height: 8,
          borderRadius: 4,
          backgroundColor: !isOnline ? "#f0b429" : "#1ee087"
        }} />
        {!isOnline ? (
          <span><strong>Offline mode:</strong> Transactions are saved locally in your phone&apos;s queue.</span>
        ) : (
          <span><strong>{pendingCount} offline transaction{pendingCount > 1 ? "s" : ""}</strong> ready to sync.</span>
        )}
      </div>

      {isOnline && pendingCount > 0 && (
        <button
          className="btn"
          style={{ padding: "4px 12px", fontSize: "0.8rem", height: "auto" }}
          disabled={isSyncing}
          onClick={() => void syncNow()}
        >
          {isSyncing ? "Syncing…" : "Sync to ledger"}
        </button>
      )}
    </div>
  );
}
