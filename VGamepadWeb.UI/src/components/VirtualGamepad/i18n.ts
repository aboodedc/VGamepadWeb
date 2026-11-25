export type Language = 'ar' | 'en';

export const translations = {
  ar: {
    width: 'العرض', height: 'الارتفاع', diameter: 'القطر', keepProportions: 'الحفاظ على النسب',
    command: 'زر اليد المرسل', appearance: 'المظهر', iconStyle: 'أيقونة', padStyle: 'لوحة ملونة',
    padLabel: 'التسمية', padColor: 'اللون', screenOptions: 'خيارات الشاشة', standalone: 'تطبيق مستقل',
    standaloneActive: 'أنت تستخدم وضع التطبيق المستقل بالفعل. هذا يختلف عن ملء الشاشة الأصلي.',
    fullscreenUnavailable: 'هذا المتصفح لا يتيح ملء الشاشة لهذه الصفحة. لم يتم تفعيل ملء الشاشة.',
    fullscreenRejected: 'لم يسمح المتصفح بملء الشاشة. يمكنك المحاولة مجددًا بالزر أو استخدام وضع اللعب.',
    details: 'تفاصيل الخطأ', gameMode: 'وضع اللعب',
    gameModeDescription: 'يخفي وضع اللعب شريط التحكم الخاص بالتطبيق. قد تبقى أشرطة المتصفح ظاهرة.',
    homeScreenTitle: 'على iPhone: أضف إلى الشاشة الرئيسية',
    homeScreenInstructions: 'افتح هذا الرابط في Safari، ثم مشاركة ← إضافة إلى الشاشة الرئيسية. فعّل «فتح كتطبيق ويب» إن ظهر، وافتح التطبيق من الأيقونة الجديدة.',
    homeScreenStorage: 'قد يختلف التخزين بين المتصفح والتطبيق المستقل. صدّر تخطيطك الحالي ثم استورده هناك إذا لزم الأمر.',
    showToolbar: 'إظهار شريط التحكم',
    rotateForDrums: 'أدر الهاتف أفقيًا لاستخدام لوحات الطبول بكلتا اليدين.',
    // Connection status
    connected: 'متصل',
    connecting: 'جاري...',
    disconnected: 'غير متصل',

    // TopBar buttons
    hide: '👁️ إخفاء',
    minimize: '🗗 تصغير',
    fullscreen: '🗖 كاملة',
    done: '✓ تم',
    edit: '✏️ تعديل',
    touchArea: '🔲 منطقة اللمس',
    buttons: '👁️ الأزرار',
    leftStick: 'عصا اليسار',
    rightStick: 'عصا اليمين',

    // Menu
    reset: '🔄 إعادة ضبط',
    export: '📤 تصدير',
    import: '📥 استيراد',
    connection: '⚙️ اتصال',

    // Settings Modal
    settings: 'الإعدادات',
    profile: 'البروفايل (تخصيص الأزرار)',
    buttonStyle: 'تخطيط التحكم',
    xboxDefault: 'Xbox (الافتراضي)',
    controllerTypeLabel: 'نوع اليد (التي تظهر في النظام)',
    enableVibration: 'تفعيل الاهتزاز',
    sensitivity: 'حساسية الأزرار/العصا',
    useSameServer: 'استخدام الخادم من نفس رابط الموقع',
    customServer: 'عنوان السيرفر المخصص',
    password: 'كلمة المرور (اختياري)',
    passwordPlaceholder: 'اتركها فارغة إذا لم يكن هناك باسورد',
    disconnect: 'قطع الاتصال',
    connect: 'اتصال',
    close: 'إغلاق',

    // Profile prompts/alerts
    newProfilePrompt: 'أدخل اسم البروفايل الجديد:',
    cannotDeleteOnly: 'لا يمكن حذف البروفايل الوحيد',
    confirmDeleteProfile: 'هل أنت متأكد من حذف هذا البروفايل؟',
    invalidFile: 'ملف غير صالح',
    readFileFailed: 'فشل قراءة الملف',

    // EditBar
    leftStickFull: 'عصا التحكم اليسرى (LS)',
    rightStickFull: 'عصا التحكم اليمنى (RS)',
    buttonPrefix: 'زر',
    size: 'الحجم',
    shape: 'الشكل',
    circular: '● دائري',
    rectangle: '■ مستطيل',
    position: 'الموقع',
    align: 'محاذاة',
    resetButton: '🔄 إعادة تعيين الزر',
    closePanel: '✕ إغلاق',
    editHint: 'اختر أي زر بالضغط عليه لتغيير حجمه وشكله ومكانه بدقة',

    // Language menu
    language: '🌐 اللغة',
    arabic: 'العربية',
    english: 'English',

    // About
    about: 'ℹ️ حول',
    aboutTitle: 'VGamepad Web',
    aboutMadeWith: 'صُنع بكل ❤️ من',
    aboutEmail: 'alhatab.edc@gmail.com',
    aboutName: 'Abdullrahman Alhatab',
    aboutGithubStar: '⭐ أعجبك؟ أضف نجمة على GitHub',
    aboutLinkedIn: '💼 LinkedIn',
    aboutMIT: 'محمي برخصة MIT المفتوحة',
  },
  en: {
    width: 'Width', height: 'Height', diameter: 'Diameter', keepProportions: 'Keep proportions',
    command: 'Gamepad command', appearance: 'Appearance', iconStyle: 'Controller icon', padStyle: 'Colored pad',
    padLabel: 'Label', padColor: 'Color', screenOptions: 'Screen options', standalone: 'Standalone',
    standaloneActive: 'You are already running as a standalone web app. This is separate from native fullscreen.',
    fullscreenUnavailable: 'This browser does not offer fullscreen for this page. Native fullscreen has not been activated.',
    fullscreenRejected: 'The browser did not allow fullscreen. Try the button again or use Game mode.',
    details: 'Error details', gameMode: 'Game mode',
    gameModeDescription: 'Game mode hides the app toolbar. Browser bars may remain visible.',
    homeScreenTitle: 'On iPhone: Add to Home Screen',
    homeScreenInstructions: 'Open this address in Safari, then Share → Add to Home Screen. Enable “Open as Web App” if offered, then launch from the new icon.',
    homeScreenStorage: 'Browser and standalone storage may differ. Export your current layout and import it there if needed.',
    showToolbar: 'Show toolbar',
    rotateForDrums: 'Rotate your phone to landscape to play drums with both thumbs.',
    // Connection status
    connected: 'Connected',
    connecting: 'Connecting...',
    disconnected: 'Disconnected',

    // TopBar buttons
    hide: '👁️ Hide',
    minimize: '🗗 Exit',
    fullscreen: '🗖 Fullscreen',
    done: '✓ Done',
    edit: '✏️ Edit',
    touchArea: '🔲 Hitboxes',
    buttons: '👁️ Buttons',
    leftStick: 'Left Stick',
    rightStick: 'Right Stick',

    // Menu
    reset: '\u{1F501} Reset',
    export: '📤 Export',
    import: '📥 Import',
    connection: '⚙️ Connection',

    // Settings Modal
    settings: 'Settings',
    profile: 'Profile (Button Layout)',
    buttonStyle: 'Controller layout',
    xboxDefault: 'Xbox (Default)',
    controllerTypeLabel: 'Controller Type (shown in system)',
    enableVibration: 'Enable Vibration',
    sensitivity: 'Button / Stick Sensitivity',
    useSameServer: 'Use same server as website URL',
    customServer: 'Custom Server URL',
    password: 'Password (optional)',
    passwordPlaceholder: 'Leave empty if no password',
    disconnect: 'Disconnect',
    connect: 'Connect',
    close: 'Close',

    // Profile prompts/alerts
    newProfilePrompt: 'Enter new profile name:',
    cannotDeleteOnly: 'Cannot delete the only profile',
    confirmDeleteProfile: 'Are you sure you want to delete this profile?',
    invalidFile: 'Invalid file',
    readFileFailed: 'Failed to read file',

    // EditBar
    leftStickFull: 'Left Control Stick (LS)',
    rightStickFull: 'Right Control Stick (RS)',
    buttonPrefix: 'Button',
    size: 'Size',
    shape: 'Shape',
    circular: '● Circle',
    rectangle: '■ Rectangle',
    position: 'Position',
    align: 'Align',
    resetButton: '🔄 Reset Button',
    closePanel: '✕ Close',
    editHint: 'Tap any button to adjust its size, shape, and position precisely',

    // Language menu
    language: '🌐 Language',
    arabic: 'العربية',
    english: 'English',

    // About
    about: 'ℹ️ About',
    aboutTitle: 'VGamepad Web',
    aboutMadeWith: 'Made with ❤️ by',
    aboutEmail: 'alhatab.edc@gmail.com',
    aboutName: 'Abdullrahman Alhatab',
    aboutGithubStar: '⭐ Like it? Star on GitHub',
    aboutLinkedIn: '💼 LinkedIn',
    aboutMIT: 'Licensed under MIT Open Source',
  },
} as const;

export type Translations = typeof translations['en'];
