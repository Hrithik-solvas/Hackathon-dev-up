import React from 'react';
import ErrorPage from '../../features/error/ErrorPage';

interface Props {
  children: React.ReactNode;
}

interface State {
  hasError: boolean;
  message: string;
}

export default class ErrorBoundary extends React.Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, message: '' };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, message: error?.message ?? 'An unexpected error occurred.' };
  }

  handleReset = () => {
    this.setState({ hasError: false, message: '' });
  };

  render() {
    if (this.state.hasError) {
      return (
        <ErrorPage
          variant="error"
          message={this.state.message}
          onTryAgain={this.handleReset}
        />
      );
    }
    return this.props.children;
  }
}
