import React, { useEffect, useRef, useState } from 'react';
import { Bus, KeyRound, UserCheck } from 'lucide-react';
import { UserProfile, UserRole } from '../types';
import { Badge } from './ui/Badge';
import { IconButton } from './ui/IconButton';
import { describeCountdown, formatCountdown } from '../lib/format';

type TabId = 'search' | 'my-bookings' | 'operator';

interface HeaderProps {
  activeHoldExpiry: string | null;
  activeTab: TabId;
  onTabChange: (tab: TabId) => void;
  currentUser: UserProfile | null;
  onOpenAuth: () => void;
}

interface TabDescriptor {
  id: TabId;
  label: string;
  panelId: string;
}

const TABS: TabDescriptor[] = [
  { id: 'search', label: 'Search & book', panelId: 'panel-search' },
  { id: 'my-bookings', label: 'My bookings', panelId: 'panel-my-bookings' },
  { id: 'operator', label: 'Operator console', panelId: 'panel-operator' },
];

/**
 * Departure board masthead: brand mark, section tablist, and the passenger's
 * live status (an active seat hold, whether they're signed in).
 */
export const Header: React.FC<HeaderProps> = ({
  activeHoldExpiry,
  activeTab,
  onTabChange,
  currentUser,
  onOpenAuth,
}) => {
  const tabRefs = useRef<Partial<Record<TabId, HTMLButtonElement | null>>>({});
  const [now, setNow] = useState(() => Date.now());

  // Ticks once a second only while a hold is actually outstanding, and stops
  // itself the moment the hold elapses rather than counting on forever.
  useEffect(() => {
    if (!activeHoldExpiry) return undefined;

    const expiryMs = new Date(activeHoldExpiry).getTime();
    if (Number.isNaN(expiryMs)) return undefined;

    setNow(Date.now());
    const intervalId = window.setInterval(() => {
      setNow(Date.now());
      if (Date.now() >= expiryMs) {
        window.clearInterval(intervalId);
      }
    }, 1000);

    return () => window.clearInterval(intervalId);
  }, [activeHoldExpiry]);

  const holdExpiryMs = activeHoldExpiry ? new Date(activeHoldExpiry).getTime() : null;
  const holdRemainingSeconds =
    holdExpiryMs !== null && !Number.isNaN(holdExpiryMs)
      ? Math.max(0, Math.round((holdExpiryMs - now) / 1000))
      : null;
  // The server told us an expiry, not a duration. Once it has passed there is
  // nothing true left to count down, so the indicator disappears instead of
  // sitting on a stale "00:00".
  const hasActiveHold = holdRemainingSeconds !== null && holdRemainingSeconds > 0;

  const isOperatorUser = Boolean(
    currentUser?.roles?.some((r) => r === UserRole.Admin || r === UserRole.Operator)
  );
  const visibleTabs = TABS.filter((tab) => tab.id !== 'operator' || isOperatorUser);

  const focusTab = (id: TabId) => tabRefs.current[id]?.focus();

  const handleTabKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>, index: number) => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    event.preventDefault();
    const direction = event.key === 'ArrowRight' ? 1 : -1;
    const next = visibleTabs[(index + direction + visibleTabs.length) % visibleTabs.length];
    onTabChange(next.id);
    focusTab(next.id);
  };

  const authLabel = currentUser ? `Signed in as ${currentUser.email}` : 'Sign in';

  const holdBadge = hasActiveHold ? (
    <Badge variant="ochre" blink>
      <span aria-hidden="true">
        Seat held &middot; <span className="figures">{formatCountdown(holdRemainingSeconds ?? 0)}</span>
      </span>
      <span className="sr-only">{describeCountdown(holdRemainingSeconds ?? 0)}</span>
    </Badge>
  ) : null;

  return (
    <header className="stock sticky top-0 z-50 rounded-none border-x-0 border-t-0 px-4 py-3.5 md:px-6">
      <div className="mx-auto flex max-w-7xl flex-col gap-3 md:flex-row md:items-center md:justify-between">
        {/* Brand */}
        <div className="flex w-full items-center justify-between gap-3 md:w-auto md:justify-start">
          <div className="flex items-center gap-3">
            <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-ticket bg-signal text-surface">
              <Bus className="h-6 w-6" aria-hidden="true" />
            </span>
            <div>
              <p className="board text-2xl text-content">BitRoute</p>
              <p className="text-xs text-content-muted">Intercity bus seats, booked ahead</p>
            </div>
          </div>

          <IconButton
            label={authLabel}
            variant="bordered"
            onClick={onOpenAuth}
            className="md:hidden"
            icon={
              currentUser ? (
                <UserCheck className="h-5 w-5 text-stamp" />
              ) : (
                <KeyRound className="h-5 w-5" />
              )
            }
          />
        </div>

        {/* Active hold, mobile: full width so it can't be missed while paying */}
        {holdBadge && <div className="md:hidden">{holdBadge}</div>}

        {/* Section tabs */}
        <div
          role="tablist"
          aria-label="Main sections"
          className="flex items-center gap-1 self-start rounded-ticket border border-rule bg-surface-sunk p-1.5 md:self-auto"
        >
          {visibleTabs.map((tab, index) => {
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                ref={(el) => {
                  tabRefs.current[tab.id] = el;
                }}
                type="button"
                role="tab"
                id={`tab-${tab.id}`}
                aria-selected={isActive}
                aria-controls={tab.panelId}
                tabIndex={isActive ? 0 : -1}
                onClick={() => onTabChange(tab.id)}
                onKeyDown={(event) => handleTabKeyDown(event, index)}
                className={[
                  'rounded-ticket px-3.5 py-2 text-xs font-bold uppercase tracking-signage transition-colors',
                  isActive ? 'bg-signal text-surface' : 'text-content-muted hover:text-content',
                ].join(' ')}
              >
                {tab.label}
              </button>
            );
          })}
        </div>

        {/* Status + account, desktop */}
        <div className="hidden items-center gap-3 md:flex">
          {holdBadge}

          <button
            type="button"
            onClick={onOpenAuth}
            className="flex items-center gap-2 rounded-ticket border border-rule-strong bg-surface-raised px-3.5 py-2 text-xs font-semibold text-content transition-colors hover:bg-surface-sunk"
          >
            {currentUser ? (
              <>
                <UserCheck className="h-4 w-4 text-stamp" aria-hidden="true" />
                <span>
                  Signed in as <span className="font-mono">{currentUser.email}</span>
                </span>
              </>
            ) : (
              <>
                <KeyRound className="h-4 w-4 text-content-muted" aria-hidden="true" />
                <span>Sign in</span>
              </>
            )}
          </button>
        </div>
      </div>
    </header>
  );
};
