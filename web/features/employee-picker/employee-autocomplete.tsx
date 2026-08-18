'use client';

import { useCallback, useEffect, useId, useRef, useState } from 'react';
import type { Employee } from '@entities/employee';
import { searchEmployeesClient, createEmployeeClient } from '@entities/employee/api.client';
import { ApiError } from '@shared/api';
import { toast } from '@shared/ui/toast';

type Props = {
  organizationId: string;
  name: string;
  label?: string;
  required?: boolean;
  error?: string;
  value?: Employee | null;
  onSelect?: (employee: Employee | null) => void;
};

export function EmployeeAutocomplete({
  organizationId,
  name,
  label = 'Assignee',
  required,
  error,
  value = null,
  onSelect,
}: Props) {
  const inputId = useId();
  const [text, setText] = useState(value?.name ?? '');
  const [selected, setSelected] = useState<Employee | null>(value);
  const [options, setOptions] = useState<Employee[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [creating, setCreating] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (selected) return;
    const q = text.trim();
    const controller = new AbortController();
    abortRef.current?.abort();
    abortRef.current = controller;
    setLoading(true);
    const t = setTimeout(async () => {
      try {
        const rows = await searchEmployeesClient(
          organizationId,
          q === '' ? undefined : q,
          10,
          controller.signal,
        );
        setOptions(rows);
      } catch (e) {
        if ((e as Error).name !== 'AbortError') {
          const msg = e instanceof ApiError ? e.problem.title : 'Search failed';
          toast.error('Could not search employees', msg);
          setOptions([]);
        }
      } finally {
        setLoading(false);
      }
    }, 200);
    return () => {
      clearTimeout(t);
      controller.abort();
    };
  }, [text, selected, organizationId]);

  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (!containerRef.current?.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', onClick);
    return () => document.removeEventListener('mousedown', onClick);
  }, []);

  const select = useCallback((e: Employee) => {
    setSelected(e);
    setText(e.name);
    setOpen(false);
    onSelect?.(e);
  }, [onSelect]);

  const clear = () => {
    setSelected(null);
    setText('');
    setOpen(true);
    onSelect?.(null);
  };

  const create = async () => {
    const name = text.trim();
    if (!name) return;
    setCreating(true);
    try {
      const key = typeof crypto !== 'undefined' && 'randomUUID' in crypto
        ? crypto.randomUUID()
        : undefined;
      const { id } = await createEmployeeClient(organizationId, { name }, key);
      const created: Employee = { id, name, email: null, phone: null, createdAt: new Date().toISOString() };
      select(created);
      toast.success('Employee created', name);
    } catch (e) {
      const msg = e instanceof ApiError ? (e.problem.detail ?? e.problem.title) : (e as Error).message;
      toast.error('Could not create employee', msg);
    } finally {
      setCreating(false);
    }
  };

  const exactMatch = options.some((o) => o.name.toLowerCase() === text.trim().toLowerCase());
  const canCreate = !selected && text.trim().length >= 2 && !exactMatch;

  return (
    <div className="flex flex-col gap-1" ref={containerRef}>
      <label htmlFor={inputId} className="text-sm font-medium text-slate-700">
        {label}
      </label>
      <div
        className="relative"
        role="combobox"
        aria-haspopup="listbox"
        aria-controls={`${inputId}-listbox`}
        aria-expanded={open}
      >
        <input
          id={inputId}
          type="text"
          value={text}
          required={required}
          onChange={(e) => {
            setSelected(null);
            setText(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          placeholder="Search or create an employee…"
          aria-autocomplete="list"
          aria-controls={`${inputId}-listbox`}
          className={[
            'w-full rounded-md border bg-white px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 shadow-sm focus:outline-none focus:ring-2 focus:ring-offset-1',
            error
              ? 'border-red-400 focus:ring-red-400'
              : 'border-slate-300 focus:border-brand-500 focus:ring-brand-500',
          ].join(' ')}
        />
        <input type="hidden" name={name} value={selected?.id ?? ''} />
        {selected && (
          <button
            type="button"
            onClick={clear}
            aria-label="Clear employee"
            className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-700"
          >
            &times;
          </button>
        )}

        {open && !selected && (
          <ul
            id={`${inputId}-listbox`}
            role="listbox"
            className="absolute z-20 mt-1 max-h-64 w-full overflow-auto rounded-md border border-slate-200 bg-white shadow-lg"
          >
            {loading && (
              <li className="px-3 py-2 text-xs text-slate-500">Searching…</li>
            )}
            {!loading && options.length === 0 && text.trim() === '' && (
              <li className="px-3 py-2 text-xs text-slate-500">Type to search employees.</li>
            )}
            {!loading && options.length === 0 && text.trim() !== '' && !canCreate && (
              <li className="px-3 py-2 text-xs text-slate-500">No matches.</li>
            )}
            {options.map((o) => (
              <li key={o.id} role="option" aria-selected={false}>
                <button
                  type="button"
                  onClick={() => select(o)}
                  className="flex w-full flex-col items-start px-3 py-2 text-left text-sm hover:bg-brand-50 focus:bg-brand-50 focus:outline-none"
                >
                  <span className="font-medium text-slate-900">{o.name}</span>
                  {(o.email || o.phone) && (
                    <span className="text-xs text-slate-500">
                      {[o.email, o.phone].filter(Boolean).join(' · ')}
                    </span>
                  )}
                </button>
              </li>
            ))}
            {canCreate && (
              <li>
                <button
                  type="button"
                  onClick={create}
                  disabled={creating}
                  className="flex w-full items-center gap-2 border-t border-slate-100 px-3 py-2 text-left text-sm text-brand-700 hover:bg-brand-50 disabled:opacity-60"
                >
                  <span aria-hidden>+</span>
                  <span>
                    Create employee &ldquo;<strong>{text.trim()}</strong>&rdquo;
                  </span>
                </button>
              </li>
            )}
          </ul>
        )}
      </div>
      {error && <p className="text-xs text-red-600">{error}</p>}
    </div>
  );
}
