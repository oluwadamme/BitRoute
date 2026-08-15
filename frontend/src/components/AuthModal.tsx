import React, { useState } from 'react';
import { ArrowLeft, LogIn, LogOut, Shield, Ticket, UserCheck, UserPlus } from 'lucide-react';
import { api, toErrorMessage } from '../services/api';
import { UserProfile, UserRole } from '../types';
import { Modal } from './ui/Modal';
import { Button } from './ui/Button';
import { Field, controlStyles } from './ui/Field';
import { NotificationBanner, Notification } from './ui/NotificationBanner';
import { shortRef } from '../lib/format';

interface AuthModalProps {
  currentUser: UserProfile | null;
  onClose: () => void;
  onAuthSuccess: (user: UserProfile | null) => void;
  initialMode?: 'login' | 'register';
  onModeChange?: (mode: 'login' | 'register') => void;
}

type Mode = 'login' | 'register' | 'provision';

const MIN_PASSWORD_LENGTH = 8;

/**
 * One fallback per mode. A single shared string told an admin whose operator
 * provisioning failed to "check your email and password", which describes a
 * sign-in attempt they never made. The fallback only shows when the server
 * sent nothing usable — a real API message always wins.
 */
const SUBMIT_FALLBACK: Record<Mode, string> = {
  login: "We couldn't sign you in. Check your email and password, then try again.",
  register: "We couldn't create your account. Check your details, then try again.",
  provision: "We couldn't create that operator account. Check the details, then try again.",
};

export const AuthModal: React.FC<AuthModalProps> = ({
  currentUser,
  onClose,
  onAuthSuccess,
  initialMode = 'login',
  onModeChange,
}) => {
  const [mode, setMode] = useState<Mode>(initialMode);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [notification, setNotification] = useState<Notification | null>(null);
  const [passwordTouched, setPasswordTouched] = useState(false);
  const [attemptedSubmit, setAttemptedSubmit] = useState(false);

  // An admin who has stepped into provisioning is still "signed in" — only
  // show the profile pane when they are not in the middle of that flow.
  const showProfile = Boolean(currentUser) && mode !== 'provision';

  const passwordTooShort = password.length > 0 && password.length < MIN_PASSWORD_LENGTH;
  const passwordError =
    (passwordTouched || attemptedSubmit) && passwordTooShort
      ? `Password needs at least ${MIN_PASSWORD_LENGTH} characters.`
      : null;

  const resetFormState = () => {
    setNotification(null);
    setAttemptedSubmit(false);
    setPasswordTouched(false);
  };

  const switchMode = (nextMode: 'login' | 'register') => {
    setMode(nextMode);
    onModeChange?.(nextMode);
    resetFormState();
  };

  const enterProvisionMode = () => {
    setMode('provision');
    setEmail('');
    setPassword('');
    setFullName('');
    resetFormState();
  };

  const exitProvisionMode = () => {
    setMode('login');
    setEmail('');
    setPassword('');
    setFullName('');
    resetFormState();
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setAttemptedSubmit(true);
    if (password.length < MIN_PASSWORD_LENGTH) return;

    setIsSubmitting(true);
    setNotification(null);

    try {
      if (mode === 'login') {
        await api.login({ email, password });
        const user = await api.getCurrentUser();
        onAuthSuccess(user);
        onClose();
      } else if (mode === 'register') {
        await api.register({ email, password, fullName });
        const user = await api.getCurrentUser();
        onAuthSuccess(user);
        onClose();
      } else {
        await api.provisionOperator({ email, password, fullName });
        setNotification({ type: 'success', message: `Operator account created for ${email}.` });
        setEmail('');
        setPassword('');
        setFullName('');
        setAttemptedSubmit(false);
        setPasswordTouched(false);
      }
    } catch (err: unknown) {
      setNotification({
        type: 'error',
        message: toErrorMessage(err, SUBMIT_FALLBACK[mode]),
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleLogout = async () => {
    await api.logout();
    onAuthSuccess(null);
    onClose();
  };

  const modalTitle = showProfile ? (
    <div className="flex items-center gap-2">
      <UserCheck aria-hidden="true" className="w-5 h-5 text-stamp-deep" />
      <span className="board text-xl text-content">Your account</span>
    </div>
  ) : (
    <div className="flex items-center gap-2">
      <Ticket aria-hidden="true" className="w-5 h-5 text-signal-deep" />
      <span className="board text-xl text-content">
        {mode === 'login' ? 'Sign in' : mode === 'register' ? 'Create your account' : 'Add an operator'}
      </span>
    </div>
  );

  const titleText = showProfile
    ? 'Your account'
    : mode === 'login'
    ? 'Sign in'
    : mode === 'register'
    ? 'Create your account'
    : 'Add an operator';

  return (
    <Modal isOpen={true} onClose={onClose} title={modalTitle} titleText={titleText}>
      {showProfile && currentUser ? (
        <div className="space-y-5">
          <div className="rounded-ticket border border-rule bg-surface-sunk p-4 space-y-3 text-sm">
            <div className="flex justify-between gap-4">
              <span className="stencil text-content-faint">Account ID</span>
              {/*
                A 36-character UUID is noise on screen and unreadable aloud, so
                only the short reference is shown. The full id stays selectable
                via the tooltip and is exposed in full to assistive tech for
                anyone who has to quote it to support.
              */}
              <span className="figures text-content">
                <span aria-hidden="true" title={currentUser.id}>
                  {shortRef(currentUser.id)}
                </span>
                <span className="sr-only">{currentUser.id}</span>
              </span>
            </div>
            <div className="flex justify-between gap-4">
              <span className="stencil text-content-faint">Email</span>
              <span className="text-content">{currentUser.email}</span>
            </div>
            <div className="flex justify-between gap-4">
              <span className="stencil text-content-faint">Account type</span>
              <span className="text-content">{currentUser.roles.join(', ') || 'Passenger'}</span>
            </div>
          </div>

          <div className="space-y-3">
            {currentUser.roles.includes(UserRole.Admin) && (
              <Button
                variant="secondary"
                className="w-full"
                onClick={enterProvisionMode}
                icon={<Shield className="w-4 h-4" />}
              >
                Add an operator
              </Button>
            )}

            <Button
              variant="danger"
              className="w-full"
              onClick={handleLogout}
              icon={<LogOut className="w-4 h-4" />}
            >
              Sign out
            </Button>
          </div>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          <NotificationBanner notification={notification} onDismiss={() => setNotification(null)} />

          {mode === 'provision' && (
            <p className="text-xs text-content-muted -mt-1">
              This creates a new operator account with access to the operator console.
            </p>
          )}

          {mode !== 'login' && (
            <Field label="Full name">
              {(ids) => (
                <input
                  {...ids}
                  type="text"
                  required
                  autoComplete="name"
                  placeholder="Amina Bello"
                  className={controlStyles}
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                />
              )}
            </Field>
          )}

          <Field label="Email address">
            {(ids) => (
              <input
                {...ids}
                type="email"
                required
                autoComplete="email"
                placeholder="you@example.com"
                className={controlStyles}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            )}
          </Field>

          <Field
            label="Password"
            hint={mode !== 'login' ? `At least ${MIN_PASSWORD_LENGTH} characters.` : undefined}
            error={passwordError}
          >
            {(ids) => (
              <input
                {...ids}
                type="password"
                required
                autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                placeholder="••••••••"
                className={controlStyles}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                onBlur={() => setPasswordTouched(true)}
              />
            )}
          </Field>

          <Button
            type="submit"
            isLoading={isSubmitting}
            loadingLabel={
              mode === 'login'
                ? 'Signing you in'
                : mode === 'register'
                ? 'Creating your account'
                : 'Creating the operator account'
            }
            className="w-full"
            icon={mode === 'login' ? <LogIn className="w-4 h-4" /> : <UserPlus className="w-4 h-4" />}
          >
            {mode === 'login' ? 'Sign in' : mode === 'register' ? 'Create account' : 'Create operator account'}
          </Button>

          {mode === 'provision' ? (
            <p className="pt-1 text-center text-xs text-content-muted">
              <button
                type="button"
                onClick={exitProvisionMode}
                className="inline-flex items-center gap-1 font-semibold text-signal-deep hover:underline"
              >
                <ArrowLeft aria-hidden="true" className="w-3.5 h-3.5" />
                Back to your account
              </button>
            </p>
          ) : (
            <p className="pt-1 text-center text-xs text-content-muted">
              {mode === 'login' ? (
                <span>
                  New to BitRoute?{' '}
                  <button
                    type="button"
                    onClick={() => switchMode('register')}
                    className="font-semibold text-signal-deep hover:underline"
                  >
                    Create an account
                  </button>
                </span>
              ) : (
                <span>
                  Already have an account?{' '}
                  <button
                    type="button"
                    onClick={() => switchMode('login')}
                    className="font-semibold text-signal-deep hover:underline"
                  >
                    Sign in
                  </button>
                </span>
              )}
            </p>
          )}
        </form>
      )}
    </Modal>
  );
};
