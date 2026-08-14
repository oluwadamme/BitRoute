/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      fontFamily: {
        // Condensed signage face, drawn for transit wayfinding. Headlines only.
        display: ['"Big Shoulders Display"', 'Haettenschweiler', 'Impact', 'sans-serif'],
        // Sturdy grotesque that holds up at small sizes. All body copy.
        sans: ['Archivo', 'Helvetica Neue', 'Helvetica', 'sans-serif'],
        // Ticket stock and timetable figures. Fares, indices, references.
        mono: ['"IBM Plex Mono"', 'ui-monospace', 'Menlo', 'monospace'],
      },
      colors: {
        // Warm newsprint ground, never pure white.
        paper: {
          DEFAULT: '#F2EDE3',
          raised: '#FBF8F2',
          sunk: '#E7E0D2',
        },
        ink: {
          DEFAULT: '#16130E',
          muted: '#5B5347',
          faint: '#8C8371',
        },
        rule: {
          DEFAULT: '#D6CDBC',
          strong: '#BCB09A',
        },
        // Signage vermilion. The single hot accent, used sparingly.
        signal: {
          DEFAULT: '#D6452B',
          deep: '#A82F1B',
          wash: '#FBE7E2',
        },
        // Deep ink-teal for settled, confirmed states.
        stamp: {
          DEFAULT: '#1F4E5F',
          deep: '#143743',
          wash: '#E1ECEF',
        },
        // Ochre for pending holds and anything on a countdown.
        ochre: {
          DEFAULT: '#B57F1E',
          deep: '#8A5F13',
          wash: '#F7EEDA',
        },
      },
      borderRadius: {
        ticket: '3px',
      },
      boxShadow: {
        // Printed stock sits on the page, it does not float above it.
        stub: '0 1px 0 0 #D6CDBC, 0 2px 0 0 rgba(22, 19, 14, 0.04)',
        raised: '0 2px 0 0 #D6CDBC, 0 6px 16px -8px rgba(22, 19, 14, 0.28)',
        press: 'inset 0 2px 0 0 rgba(22, 19, 14, 0.10)',
      },
      letterSpacing: {
        signage: '0.02em',
        stencil: '0.14em',
      },
      keyframes: {
        'stub-in': {
          '0%': { opacity: '0', transform: 'translateY(6px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'roll-in': {
          '0%': { opacity: '0', transform: 'translateY(-0.35em)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'tick': {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.25' },
        },
      },
      animation: {
        'stub-in': 'stub-in 420ms cubic-bezier(0.2, 0.7, 0.3, 1) both',
        'roll-in': 'roll-in 300ms cubic-bezier(0.2, 0.7, 0.3, 1) both',
        // Departure-board blink. One second, not a perpetual pulse.
        'tick': 'tick 1.1s steps(1, end) infinite',
      },
    },
  },
  plugins: [],
};
