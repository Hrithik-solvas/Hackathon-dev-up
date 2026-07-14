import { createTheme } from '@mui/material/styles';

/**
 * Solvas Design System Tokens (from Figma Component Library)
 *
 * Primary Blue:    #01579B (main), #0288D1 (light), #013654 (dark)
 * Teal/Accent:     #1F6473
 * Green/Success:   #2E7D32 (main), #1B5E20 (dark), #EAF2EA (light bg)
 * Error/Red:       #D32F2F (main), #F44336 (light), #541313 (dark)
 * Warning/Orange:  #ED6C02 (main), #FFA726 (light), #5F2B01 (dark)
 * Info/Blue:       #0288D1 (main), #C2EDFF (light bg)
 * Purple:          #7B61FF (main), #E3DEFF (light bg)
 *
 * Neutrals:
 *   Black:         #222222
 *   Dark gray:     #53565A
 *   Medium gray:   #757575
 *   Gray:          #8F8F8F
 *   Light gray:    #B4B7B7
 *   Border:        #D9D9D9
 *   Background:    #E7E7E7, #F0F0F0, #F5F5F5, #F7F7F7
 *   White:         #FFFFFF
 */

const solvasTheme = createTheme({
  palette: {
    primary: {
      main: '#01579B',
      light: '#0288D1',
      dark: '#013654',
      contrastText: '#FFFFFF',
    },
    secondary: {
      main: '#1F6473',
      light: '#7DBCDF',
      dark: '#013654',
      contrastText: '#FFFFFF',
    },
    success: {
      main: '#2E7D32',
      light: '#EAF2EA',
      dark: '#1B5E20',
      contrastText: '#FFFFFF',
    },
    error: {
      main: '#D32F2F',
      light: '#FBEAEA',
      dark: '#541313',
      contrastText: '#FFFFFF',
    },
    warning: {
      main: '#ED6C02',
      light: '#FFEADA',
      dark: '#5F2B01',
      contrastText: '#FFFFFF',
    },
    info: {
      main: '#0288D1',
      light: '#E6F3FA',
      dark: '#01579B',
      contrastText: '#FFFFFF',
    },
    background: {
      default: '#F5F5F5',
      paper: '#FFFFFF',
    },
    text: {
      primary: '#222222',
      secondary: '#53565A',
      disabled: '#8F8F8F',
    },
    divider: '#D9D9D9',
    grey: {
      50: '#F7F7F7',
      100: '#F5F5F5',
      200: '#F0F0F0',
      300: '#E7E7E7',
      400: '#D9D9D9',
      500: '#B4B7B7',
      600: '#8F8F8F',
      700: '#757575',
      800: '#53565A',
      900: '#222222',
    },
  },
  typography: {
    fontFamily: '"Inter", sans-serif',
    fontSize: 13,
    h1: { fontSize: 22, fontWeight: 700, color: '#222222' },
    h2: { fontSize: 18, fontWeight: 700, color: '#222222' },
    h3: { fontSize: 16, fontWeight: 700, color: '#222222' },
    h4: { fontSize: 14, fontWeight: 700, color: '#222222' },
    body1: { fontSize: 13, color: '#222222' },
    body2: { fontSize: 12, color: '#53565A' },
    caption: { fontSize: 10, color: '#757575' },
  },
  shape: {
    borderRadius: 6,
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: `
        *, *::before, *::after { box-sizing: border-box; }
        body { margin: 0; background-color: #F5F5F5; }
        code, pre { font-family: 'JetBrains Mono', monospace; }
        ::-webkit-scrollbar { width: 6px; height: 6px; }
        ::-webkit-scrollbar-track { background: transparent; }
        ::-webkit-scrollbar-thumb { background: #B4B7B7; border-radius: 3px; }
        ::-webkit-scrollbar-thumb:hover { background: #757575; }
      `,
    },
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          borderRadius: 6,
          fontSize: 12,
          fontWeight: 600,
          boxShadow: 'none',
          '&:hover': { boxShadow: 'none' },
        },
        containedPrimary: {
          backgroundColor: '#01579B',
          '&:hover': { backgroundColor: '#013654' },
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          boxShadow: '0 2px 8px rgba(0,0,0,0.05)',
        },
      },
    },
    MuiTableHead: {
      styleOverrides: {
        root: {
          '& .MuiTableCell-head': {
            color: '#FFFFFF',
            fontSize: 11,
            fontWeight: 600,
            backgroundColor: '#01579B',
            padding: '10px 16px',
          },
        },
      },
    },
    MuiTableBody: {
      styleOverrides: {
        root: {
          '& .MuiTableRow-root:nth-of-type(odd)': {
            backgroundColor: '#FFFFFF',
          },
          '& .MuiTableRow-root:nth-of-type(even)': {
            backgroundColor: '#F7F7F7',
          },
          '& .MuiTableRow-root:hover': {
            backgroundColor: '#E6F3FA',
          },
        },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        root: {
          fontSize: 12,
          padding: '10px 16px',
          borderColor: '#E7E7E7',
          color: '#222222',
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        notchedOutline: {
          borderColor: '#D9D9D9',
        },
        root: {
          fontSize: 13,
          '&:hover .MuiOutlinedInput-notchedOutline': {
            borderColor: '#8F8F8F',
          },
          '&.Mui-focused .MuiOutlinedInput-notchedOutline': {
            borderColor: '#01579B',
          },
        },
      },
    },
    MuiInputLabel: {
      styleOverrides: {
        root: {
          fontSize: 13,
          '&.Mui-focused': { color: '#01579B' },
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          borderRadius: 9,
          fontSize: 10,
          fontWeight: 500,
          height: 20,
        },
      },
    },
    MuiSnackbar: {
      defaultProps: {
        anchorOrigin: { vertical: 'bottom', horizontal: 'right' },
      },
    },
    MuiAlert: {
      styleOverrides: {
        root: {
          fontSize: 13,
        },
      },
    },
  },
});

export default solvasTheme;
