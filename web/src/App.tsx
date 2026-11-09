import { lazy, type ReactNode } from 'react'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AppShell } from './components/layout/AppShell'
import { ToastProvider } from './components/ui/Toast'
import { SessionProvider } from './app/session'
import { routeIndex } from './app/navigation'
import { ApiError } from './lib/api'
import { LoginPage } from './features/auth/LoginPage'
import { ChangePasswordPage } from './features/auth/ChangePasswordPage'
import { PlaceholderPage } from './features/common/PlaceholderPage'

// Each module is its own chunk, loaded when first opened.
const DashboardPage = lazy(() => import('./features/dashboard/DashboardPage').then((m) => ({ default: m.DashboardPage })))
const UsersPage = lazy(() => import('./features/admin/UsersPage').then((m) => ({ default: m.UsersPage })))
const RolesPage = lazy(() => import('./features/admin/RolesPage').then((m) => ({ default: m.RolesPage })))
const OrganizationPage = lazy(() => import('./features/admin/OrganizationPage').then((m) => ({ default: m.OrganizationPage })))
const PatientsPage = lazy(() => import('./features/patients/PatientsPage').then((m) => ({ default: m.PatientsPage })))
const PatientProfilePage = lazy(() => import('./features/patients/PatientProfilePage').then((m) => ({ default: m.PatientProfilePage })))
const DoctorsPage = lazy(() => import('./features/patients/DoctorsPage').then((m) => ({ default: m.DoctorsPage })))
const TestsPage = lazy(() => import('./features/catalog/TestsPage').then((m) => ({ default: m.TestsPage })))
const TestEditorPage = lazy(() => import('./features/catalog/TestEditorPage').then((m) => ({ default: m.TestEditorPage })))
const CategoriesPage = lazy(() => import('./features/catalog/CategoriesPage').then((m) => ({ default: m.CategoriesPage })))
const PackagesPage = lazy(() => import('./features/catalog/PackagesPage').then((m) => ({ default: m.PackagesPage })))
const RangeApprovalPage = lazy(() => import('./features/catalog/RangeApprovalPage').then((m) => ({ default: m.RangeApprovalPage })))
const NewOrderPage = lazy(() => import('./features/orders/NewOrderPage').then((m) => ({ default: m.NewOrderPage })))
const OrdersPage = lazy(() => import('./features/orders/OrdersPage').then((m) => ({ default: m.OrdersPage })))
const OrderDetailPage = lazy(() => import('./features/orders/OrderDetailPage').then((m) => ({ default: m.OrderDetailPage })))
const PaymentsPage = lazy(() => import('./features/orders/PaymentsPage').then((m) => ({ default: m.PaymentsPage })))
const CashDrawerPage = lazy(() => import('./features/orders/CashDrawerPage').then((m) => ({ default: m.CashDrawerPage })))

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      // Retry network blips, never permission or validation errors.
      retry: (count, error) => count < 1 && !(error instanceof ApiError && error.status >= 400 && error.status < 500),
    },
  },
})

// Pages are registered here as each module is built; everything else shows the "planned" placeholder.
const pages: Record<string, ReactNode> = {
  '/': <DashboardPage />,
  '/admin/users': <UsersPage />,
  '/admin/roles': <RolesPage />,
  '/admin/branches': <OrganizationPage />,
  '/patients': <PatientsPage />,
  '/doctors': <DoctorsPage />,
  '/catalog/tests': <TestsPage />,
  '/catalog/categories': <CategoriesPage />,
  '/catalog/packages': <PackagesPage />,
  '/catalog/reference-ranges': <RangeApprovalPage />,
  '/orders/new': <NewOrderPage />,
  '/orders': <OrdersPage />,
  '/billing/invoices': <OrdersPage />,
  '/billing/payments': <PaymentsPage />,
  '/billing/cash-drawer': <CashDrawerPage />,
}

const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/change-password', element: <ChangePasswordPage /> },
  {
    element: <AppShell />,
    children: [
      ...routeIndex.map((r) => ({ path: r.item.path!, element: pages[r.item.path!] ?? <PlaceholderPage /> })),
      { path: '/patients/:id', element: <PatientProfilePage /> },
      { path: '/catalog/tests/:id', element: <TestEditorPage /> },
      { path: '/orders/:id', element: <OrderDetailPage /> },
      { path: '*', element: <PlaceholderPage /> },
    ],
  },
])

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <SessionProvider>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </SessionProvider>
    </QueryClientProvider>
  )
}
