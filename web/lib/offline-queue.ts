"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";

export interface PendingTransaction {
  id: string;
  timestamp: number;
  payload: Record<string, unknown>;
  summary: string;
}

const DB_NAME = "FinanceOS_OfflineDB";
const STORE_NAME = "pending_transactions";
const DB_VERSION = 1;

function openDB(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    if (typeof window === "undefined" || !window.indexedDB) {
      reject(new Error("IndexedDB not available"));
      return;
    }

    const request = window.indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME, { keyPath: "id" });
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

export async function savePendingTransaction(payload: Record<string, unknown>, summary: string): Promise<PendingTransaction> {
  const item: PendingTransaction = {
    id: `offline_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`,
    timestamp: Date.now(),
    payload,
    summary
  };

  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readwrite");
    const store = tx.objectStore(STORE_NAME);
    const req = store.add(item);
    req.onsuccess = () => {
      window.dispatchEvent(new CustomEvent("financeos:offline-queue-changed"));
      resolve(item);
    };
    req.onerror = () => reject(req.error);
  });
}

export async function getPendingTransactions(): Promise<PendingTransaction[]> {
  try {
    const db = await openDB();
    return new Promise((resolve, reject) => {
      const tx = db.transaction(STORE_NAME, "readonly");
      const store = tx.objectStore(STORE_NAME);
      const req = store.getAll();
      req.onsuccess = () => resolve(req.result || []);
      req.onerror = () => reject(req.error);
    });
  } catch {
    return [];
  }
}

export async function removePendingTransaction(id: string): Promise<void> {
  const db = await openDB();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readwrite");
    const store = tx.objectStore(STORE_NAME);
    const req = store.delete(id);
    req.onsuccess = () => {
      window.dispatchEvent(new CustomEvent("financeos:offline-queue-changed"));
      resolve();
    };
    req.onerror = () => reject(req.error);
  });
}

export async function syncPendingTransactions(): Promise<{ synced: number; failed: number }> {
  const items = await getPendingTransactions();
  if (items.length === 0) return { synced: 0, failed: 0 };

  let synced = 0;
  let failed = 0;

  for (const item of items) {
    try {
      await api.createTx(item.payload);
      await removePendingTransaction(item.id);
      synced++;
    } catch {
      failed++;
    }
  }

  window.dispatchEvent(new CustomEvent("financeos:offline-queue-changed"));
  return { synced, failed };
}

export function useOfflineQueue() {
  const [pending, setPending] = useState<PendingTransaction[]>([]);
  const [isSyncing, setIsSyncing] = useState(false);
  const [isOnline, setIsOnline] = useState(typeof navigator !== "undefined" ? navigator.onLine : true);

  const refresh = useCallback(async () => {
    const items = await getPendingTransactions();
    setPending(items);
  }, []);

  const syncNow = useCallback(async () => {
    if (isSyncing) return;
    setIsSyncing(true);
    try {
      await syncPendingTransactions();
      await refresh();
    } finally {
      setIsSyncing(false);
    }
  }, [isSyncing, refresh]);

  useEffect(() => {
    void refresh();

    const handleOnline = () => {
      setIsOnline(true);
      void syncNow();
    };
    const handleOffline = () => setIsOnline(false);
    const handleQueueChange = () => void refresh();

    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);
    window.addEventListener("financeos:offline-queue-changed", handleQueueChange);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
      window.removeEventListener("financeos:offline-queue-changed", handleQueueChange);
    };
  }, [refresh, syncNow]);

  return {
    pendingCount: pending.length,
    pending,
    isSyncing,
    isOnline,
    syncNow
  };
}
