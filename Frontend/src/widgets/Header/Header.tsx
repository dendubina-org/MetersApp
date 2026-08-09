import { Toggle } from '@components/Toggle';
import { Typography } from '@components/Typography';
import { LanguageSwitcher } from '@components/LanguageSwitcher';
import { useTheme } from '@hooks/useTheme';
import { useCleanupStatus, type CleaningResult } from '@hooks/useCleanupStatus';
import { twMerge } from 'tailwind-merge';
import { useTranslation } from 'react-i18next';
import type { ParseKeys } from 'i18next';
import type { FC } from 'react';

const resultColorClass: Record<CleaningResult, string> = {
  Success: 'bg-green-500',
  Failure: 'bg-red-500',
  NotPerformed: 'bg-gray-400',
};

const resultLabel: Record<CleaningResult, ParseKeys> = {
  Success: 'header.result.success',
  Failure: 'header.result.failure',
  NotPerformed: 'header.result.notPerformed',
};

export type HeaderProps = {
  className?: string;
};

export const Header: FC<HeaderProps> = ({ className }) => {
  const { t } = useTranslation();
  const { theme, toggleTheme } = useTheme();
  const { timeRemaining, lastResult, isLoading } = useCleanupStatus();
  const isDark = theme === 'dark';

  return (
    <header
      className={twMerge(
        'flex items-center justify-between px-5 py-2',
        'bg-raised border-b border-line',
        className
      )}
    >
      <Typography
        tag='h1'
        text={t('header.title')}
        weight='bold'
        className='text-xl'
      />
      <div className='flex items-center gap-6'>
        <div className='flex items-center gap-2'>
          <span className='text-sm font-medium text-secondary'>
            {t('header.nextCleanup')}
          </span>
          <span className='text-sm font-bold tabular-nums text-regular'>
            {isLoading ? t('header.loading') : timeRemaining}
          </span>
          <span
            className={twMerge(
              'inline-block size-2 rounded-full',
              resultColorClass[lastResult]
            )}
          />
          <span className='text-xs text-secondary'>
            {t(resultLabel[lastResult])}
          </span>
        </div>
        <LanguageSwitcher />
        <div className='flex items-center gap-2'>
          <span className='text-sm font-medium text-regular'>
            {isDark ? t('header.theme.dark') : t('header.theme.light')}
          </span>
          <Toggle
            checked={isDark}
            onChange={toggleTheme}
          />
        </div>
      </div>
    </header>
  );
};
