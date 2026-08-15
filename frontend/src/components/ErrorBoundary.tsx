import React from 'react';
import { AlertTriangle } from 'lucide-react';

interface ErrorBoundaryProps {
  children: React.ReactNode;
}

interface ErrorBoundaryState {
  error: Error | null;
}

/**
 * Catches render-phase errors so one bad component cannot blank the page.
 *
 * This matters most on the booking path: a passenger mid-checkout who hits a
 * white screen has no idea whether their seat is held or their card was
 * charged. A visible failure with a way out is the minimum.
 *
 * Note this only catches errors thrown during render, in lifecycle methods and
 * in constructors. Rejected promises inside event handlers still need their own
 * try/catch at the call site.
 */
export class ErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo): void {
    // Replace with the real telemetry sink when one exists.
    console.error('Unhandled render error:', error, info.componentStack);
  }

  private handleReset = (): void => {
    this.setState({ error: null });
  };

  render(): React.ReactNode {
    const { error } = this.state;
    if (!error) return this.props.children;

    return (
      <div
        role="alert"
        className="min-h-screen flex items-center justify-center p-6 bg-surface"
      >
        <div className="stock shadow-raised max-w-md w-full px-6 py-7 space-y-4">
          <div className="flex items-center gap-3 text-signal-deep">
            <AlertTriangle className="w-6 h-6 shrink-0" aria-hidden="true" />
            <h1 className="board text-2xl font-bold">Something went wrong</h1>
          </div>

          <p className="text-sm text-content-muted leading-relaxed">
            This page stopped working. Any seat you were holding is safe on our side,
            so try again or reload and pick up where you left off.
          </p>

          <div className="perforation my-2" />

          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={this.handleReset}
              className="px-4 py-2.5 text-sm font-semibold rounded-ticket bg-signal text-surface border border-signal-deep hover:bg-signal-deep transition-colors"
            >
              Try again
            </button>
            <button
              type="button"
              onClick={() => window.location.reload()}
              className="px-4 py-2.5 text-sm font-semibold rounded-ticket bg-surface-raised text-content border border-rule-strong hover:bg-surface-sunk transition-colors"
            >
              Reload the page
            </button>
          </div>

          {import.meta.env.DEV && (
            <pre className="mt-2 p-3 bg-surface-sunk border border-rule rounded-ticket text-[11px] font-mono text-content-muted overflow-x-auto whitespace-pre-wrap">
              {error.message}
            </pre>
          )}
        </div>
      </div>
    );
  }
}
