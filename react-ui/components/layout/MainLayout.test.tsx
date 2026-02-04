/**
 * Unit tests for MainLayout component
 */
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MainLayout } from './MainLayout';

describe('MainLayout', () => {
  it('should render children content', () => {
    render(
      <MainLayout>
        <div>Test Content</div>
      </MainLayout>
    );
    expect(screen.getByText('Test Content')).toBeInTheDocument();
  });

  it('should apply default page class when no className provided', () => {
    const { container } = render(
      <MainLayout>
        <div>Content</div>
      </MainLayout>
    );
    const pageDiv = container.querySelector('.page');
    expect(pageDiv).toBeInTheDocument();
    expect(pageDiv?.className).toBe('page');
  });

  it('should apply additional className to page div when provided', () => {
    const { container } = render(
      <MainLayout className="overflow-y-auto">
        <div>Content</div>
      </MainLayout>
    );
    const pageDiv = container.querySelector('.page');
    expect(pageDiv).toBeInTheDocument();
    expect(pageDiv?.className).toBe('page overflow-y-auto');
  });

  it('should apply multiple classes when provided', () => {
    const { container } = render(
      <MainLayout className="custom-class another-class">
        <div>Content</div>
      </MainLayout>
    );
    const pageDiv = container.querySelector('.page');
    expect(pageDiv).toBeInTheDocument();
    expect(pageDiv?.className).toBe('page custom-class another-class');
  });

  it('should render hamburger button', () => {
    const { container } = render(
      <MainLayout>
        <div>Content</div>
      </MainLayout>
    );
    const hamburgerBtn = container.querySelector('.hamburger-btn');
    expect(hamburgerBtn).toBeInTheDocument();
  });

  it('should not show menu by default', () => {
    const { container } = render(
      <MainLayout>
        <div>Content</div>
      </MainLayout>
    );
    const menuOverlay = container.querySelector('.menu-overlay');
    expect(menuOverlay).not.toBeInTheDocument();
  });
});
