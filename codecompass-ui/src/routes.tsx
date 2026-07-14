import React from 'react';
import { createBrowserRouter, Navigate, Outlet } from 'react-router-dom';
import AppShell from './components/layout/AppShell';
import ChatPage from './features/chat/ChatPage';
import KnowledgePage from './features/knowledge/KnowledgePage';
import HistoryPage from './features/history/HistoryPage';
import LoginPage from './features/auth/LoginPage';
import ErrorPage from './features/error/ErrorPage';
import ErrorBoundary from './components/common/ErrorBoundary';
import { getUser } from './services/authService';

function RequireAuth() {
  const user = getUser();
  return user ? <Outlet /> : <Navigate to="/login" replace />;
}

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    element: <RequireAuth />,
    children: [
      {
        path: '/',
        element: <AppShell />,
        children: [
          { index: true, element: <ErrorBoundary><ChatPage /></ErrorBoundary> },
          { path: 'chat/:sessionId', element: <ErrorBoundary><ChatPage /></ErrorBoundary> },
          { path: 'knowledge', element: <ErrorBoundary><KnowledgePage /></ErrorBoundary> },
          { path: 'history', element: <ErrorBoundary><HistoryPage /></ErrorBoundary> },
        ],
      },
    ],
  },
  {
    path: '*',
    element: <ErrorPage variant="notfound" />,
  },
]);
