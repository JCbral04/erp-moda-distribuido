// Design Tokens — Fashion Intelligence ERP Moda
// Single source of truth for the entire visual system.

export const colors = {
  bg: {
    base:    '#090A0C',
    surface: '#111317',
    raised:  '#191C21',
    overlay: '#1E2229',
  },
  border: {
    subtle:  'rgba(255,255,255,0.06)',
    default: 'rgba(255,255,255,0.10)',
    strong:  'rgba(255,255,255,0.18)',
  },
  text: {
    primary:   '#F5F1E9',
    secondary: '#92949B',
    tertiary:  '#5A5C63',
    inverse:   '#090A0C',
  },
  accent: {
    champagne:  '#D6B77A',
    champagne2: '#A58B62',
    danger:     '#E05252',
    success:    '#52C07A',
    warning:    '#D4A035',
    info:       '#5B9BD5',
  },
} as const;

export const typography = {
  family: {
    display: '"Instrument Serif", Georgia, serif',
    ui:      '"Geist", "Inter", system-ui, sans-serif',
    data:    '"Geist Mono", "JetBrains Mono", monospace',
  },
  size: {
    xs: '11px', sm: '12px', base: '14px',
    md: '16px', lg: '20px', xl: '24px',
    '2xl': '32px', '3xl': '44px', hero: '64px',
  },
  weight: { regular: 400, medium: 500, semibold: 600, bold: 700 },
  leading: { tight: 1.1, snug: 1.3, normal: 1.5, relaxed: 1.7 },
} as const;

export const spacing = {
  px: '1px', 1: '4px', 2: '8px', 3: '12px', 4: '16px',
  5: '20px', 6: '24px', 8: '32px', 10: '40px',
  12: '48px', 16: '64px', 20: '80px', 24: '96px',
} as const;

export const radius = {
  sm: '4px', md: '8px', lg: '12px', xl: '16px', pill: '9999px',
} as const;

export const shadow = {
  sm:  '0 1px 2px rgba(0,0,0,0.4)',
  md:  '0 4px 12px rgba(0,0,0,0.5)',
  lg:  '0 12px 32px rgba(0,0,0,0.6)',
  glow: '0 0 20px rgba(214,183,122,0.15)',
} as const;

export const motion = {
  duration: { instant: 80, fast: 150, normal: 250, slow: 400 },
  easing: {
    standard: 'cubic-bezier(0.4, 0, 0.2, 1)',
    enter:    'cubic-bezier(0, 0, 0.2, 1)',
    exit:     'cubic-bezier(0.4, 0, 1, 1)',
    spring:   'cubic-bezier(0.34, 1.56, 0.64, 1)',
  },
} as const;

/** Breakpoint widths (min-width) */
export const breakpoints = {
  mobile:  375,
  tablet:  768,
  desktop: 1280,
  wide:    1440,
} as const;
