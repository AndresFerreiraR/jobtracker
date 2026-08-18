'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

type Point = { x: number; y: number };

type Props = {
  width?: number;
  height?: number;
  onChange?: (dataUrl: string | null) => void;
  className?: string;
};

export function SignaturePad({ width = 480, height = 180, onChange, className }: Props) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const drawing = useRef(false);
  const last = useRef<Point | null>(null);
  const [isEmpty, setIsEmpty] = useState(true);

  const context = useCallback(() => canvasRef.current?.getContext('2d') ?? null, []);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ratio = typeof window !== 'undefined' ? window.devicePixelRatio || 1 : 1;
    canvas.width = width * ratio;
    canvas.height = height * ratio;
    canvas.style.width = `${width}px`;
    canvas.style.height = `${height}px`;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.scale(ratio, ratio);
    ctx.lineWidth = 2;
    ctx.lineCap = 'round';
    ctx.strokeStyle = '#0f172a';
  }, [width, height]);

  const emit = useCallback(() => {
    if (!canvasRef.current) return;
    if (isEmpty) {
      onChange?.(null);
      return;
    }
    onChange?.(canvasRef.current.toDataURL('image/png'));
  }, [isEmpty, onChange]);

  const pointFromEvent = (e: React.PointerEvent<HTMLCanvasElement>): Point => {
    const rect = e.currentTarget.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  };

  const start = (e: React.PointerEvent<HTMLCanvasElement>) => {
    e.preventDefault();
    e.currentTarget.setPointerCapture(e.pointerId);
    drawing.current = true;
    last.current = pointFromEvent(e);
  };

  const move = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawing.current) return;
    const ctx = context();
    if (!ctx || !last.current) return;
    const p = pointFromEvent(e);
    ctx.beginPath();
    ctx.moveTo(last.current.x, last.current.y);
    ctx.lineTo(p.x, p.y);
    ctx.stroke();
    last.current = p;
    if (isEmpty) setIsEmpty(false);
  };

  const end = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawing.current) return;
    drawing.current = false;
    last.current = null;
    try { e.currentTarget.releasePointerCapture(e.pointerId); } catch { /* noop */ }
    emit();
  };

  const clear = () => {
    const canvas = canvasRef.current;
    const ctx = context();
    if (!canvas || !ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    setIsEmpty(true);
    onChange?.(null);
  };

  return (
    <div className={className}>
      <canvas
        ref={canvasRef}
        role="img"
        aria-label="Signature pad"
        onPointerDown={start}
        onPointerMove={move}
        onPointerUp={end}
        onPointerLeave={end}
        onPointerCancel={end}
        className="touch-none rounded-md border border-gray-300 bg-white"
      />
      <div className="mt-2 flex items-center justify-between text-xs text-gray-600">
        <span>{isEmpty ? 'Sign above with mouse, finger or stylus.' : 'Signature captured.'}</span>
        <button
          type="button"
          onClick={clear}
          className="rounded border border-gray-300 bg-white px-2 py-1 hover:bg-gray-50"
        >
          Clear
        </button>
      </div>
    </div>
  );
}
