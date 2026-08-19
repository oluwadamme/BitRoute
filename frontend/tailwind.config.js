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

        surface: {
          DEFAULT: '#15120E',
          raised: '#1F1B15',
          sunk: '#0D0B08',
        },
        content: {
          DEFAULT: '#F4EFE4',
          muted: '#B3A996',
          faint: '#8A8070',
        },
        rule: {
          // Decorative hairlines and dividers; carries no state, so no 3:1 duty.
          DEFAULT: '#332D24',
          // Boundaries of real controls. Must stay >= 3:1 against surface-raised.
          strong: '#7E725F',
        },
        // Signage vermilion, lifted to glow on a dark ground. The one hot accent.
        signal: {
          DEFAULT: '#FF6A45',
          deep: '#FF8A6B',
          wash: '#2E1810',
        },
        // Cold enamel blue for settled, confirmed states.
        stamp: {
          DEFAULT: '#6FC3D6',
          deep: '#8FD4E3',
          wash: '#10262C',
        },
        // Lamp amber for pending holds and anything on a countdown.
        ochre: {
          DEFAULT: '#F0BC55',
          deep: '#F7CF7E',
          wash: '#2B2010',
        },
      },
      borderRadius: {
        ticket: '3px',
      },
      boxShadow: {
        // On a dark ground, depth reads as a lit top edge rather than a drop shadow.
        stub: 'inset 0 1px 0 0 rgba(244, 239, 228, 0.05)',
        raised:
          'inset 0 1px 0 0 rgba(244, 239, 228, 0.07), 0 12px 28px -12px rgba(0, 0, 0, 0.85)',
        press: 'inset 0 2px 0 0 rgba(0, 0, 0, 0.35)',
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
        tick: {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.25' },
        },
      },
      animation: {
        'stub-in': 'stub-in 420ms cubic-bezier(0.2, 0.7, 0.3, 1) both',
        'roll-in': 'roll-in 300ms cubic-bezier(0.2, 0.7, 0.3, 1) both',
        // Departure-board blink. One second, not a perpetual pulse.
        tick: 'tick 1.1s steps(1, end) infinite',
      },
    },
  },
  plugins: [],
};
