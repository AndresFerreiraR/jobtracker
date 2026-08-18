import { type ButtonHTMLAttributes, forwardRef } from 'react';

type Variant = 'primary' | 'secondary' | 'ghost';

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
  isLoading?: boolean;
};

const variantClasses: Record<Variant, string> = {
  primary: 'bg-brand-500 text-white hover:bg-brand-600 active:bg-brand-700 disabled:bg-brand-300',
  secondary: 'bg-brand-50 text-brand-800 hover:bg-brand-100 disabled:bg-slate-100 disabled:text-slate-400 ring-1 ring-inset ring-brand-100',
  ghost: 'bg-transparent text-brand-700 hover:bg-brand-50 disabled:text-slate-400',
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = 'primary', isLoading, disabled, className, children, ...rest },
  ref,
) {
  const finalClass = [
    'inline-flex items-center justify-center rounded-md px-4 py-2 text-sm font-medium transition-colors',
    'focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2',
    'disabled:cursor-not-allowed',
    variantClasses[variant],
    className ?? '',
  ]
    .join(' ')
    .trim();

  return (
    <button
      ref={ref}
      type="button"
      disabled={disabled || isLoading}
      className={finalClass}
      {...rest}
    >
      {isLoading ? 'Loading...' : children}
    </button>
  );
});
