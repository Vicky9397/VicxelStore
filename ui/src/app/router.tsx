import type { ReactElement } from 'react';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import { AppLayout } from '@/components/layout/AppLayout';
import { AccountPage } from '@/features/account/pages/AccountPage';
import { LoginPage } from '@/features/auth/pages/LoginPage';
import { RegisterPage } from '@/features/auth/pages/RegisterPage';
import { VerifyEmailPage } from '@/features/auth/pages/VerifyEmailPage';
import { HomePage } from '@/features/catalog/pages/HomePage';
import { ProductDetailPage } from '@/features/catalog/pages/ProductDetailPage';
import { SearchPage } from '@/features/catalog/pages/SearchPage';
import { StorePage } from '@/features/catalog/pages/StorePage';
import { CartPage } from '@/features/cart/pages/CartPage';
import { CheckoutPage } from '@/features/checkout/pages/CheckoutPage';
import { CheckoutReturnPage } from '@/features/checkout/pages/CheckoutReturnPage';
import { OrderDetailPage } from '@/features/orders/pages/OrderDetailPage';
import { PurchasesPage } from '@/features/orders/pages/PurchasesPage';
import { NewProductPage } from '@/features/seller/pages/NewProductPage';
import { ProductEditorPage } from '@/features/seller/pages/ProductEditorPage';
import { SellerProductsPage } from '@/features/seller/pages/SellerProductsPage';
import { SellerStorePage } from '@/features/seller/pages/SellerStorePage';
import { ForbiddenPage, NotFoundPage, RouteErrorPage } from '@/routes/ErrorPages';
import { RequireAuth } from '@/routes/guards';

const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'search', element: <SearchPage /> },
      { path: 'p/:slug', element: <ProductDetailPage /> },
      { path: 's/:slug', element: <StorePage /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'register', element: <RegisterPage /> },
      { path: 'verify-email', element: <VerifyEmailPage /> },
      {
        element: <RequireAuth />,
        children: [
          { path: 'account', element: <AccountPage /> },
          { path: 'account/purchases', element: <PurchasesPage /> },
          { path: 'account/orders/:orderId', element: <OrderDetailPage /> },
          { path: 'cart', element: <CartPage /> },
          { path: 'checkout', element: <CheckoutPage /> },
          { path: 'checkout/return', element: <CheckoutReturnPage /> },
          { path: 'seller/store', element: <SellerStorePage /> },
          { path: 'seller/products', element: <SellerProductsPage /> },
          { path: 'seller/products/new', element: <NewProductPage /> },
          { path: 'seller/products/:productId', element: <ProductEditorPage /> },
        ],
      },
      { path: '403', element: <ForbiddenPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);

export function AppRouter(): ReactElement {
  return <RouterProvider router={router} />;
}
