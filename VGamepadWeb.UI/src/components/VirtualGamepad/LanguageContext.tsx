import React, { createContext, useContext, useState } from 'react';
import type { Language, TranslationSet } from './i18n';
import { translations } from './i18n';

const LANGUAGES: Language[] = ['ar', 'en', 'zh'];

// 首屏：存储过的优先，否则按浏览器语言，非中文一律回落到 en。
const detectLanguage = (): Language => {
  const stored = localStorage.getItem('gamepad_language');
  if (stored && (LANGUAGES as string[]).includes(stored)) return stored as Language;
  return navigator.language.toLowerCase().startsWith('zh') ? 'zh' : 'en';
};

interface LanguageContextValue {
  language: Language;
  setLanguage: (lang: Language) => void;
  t: TranslationSet;
}

const LanguageContext = createContext<LanguageContextValue>({
  language: 'en',
  setLanguage: () => {},
  t: translations['en'] as TranslationSet,
});

export const LanguageProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [language, setLanguageState] = useState<Language>(() => {
    const lang = detectLanguage();
    // Set direction synchronously before first render to avoid flash
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.lang = lang;
    return lang;
  });

  const setLanguage = (lang: Language) => {
    setLanguageState(lang);
    localStorage.setItem('gamepad_language', lang);
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.lang = lang;
  };

  return (
    <LanguageContext.Provider value={{ language, setLanguage, t: translations[language] }}>
      {children}
    </LanguageContext.Provider>
  );
};

export const useLanguage = () => useContext(LanguageContext);
