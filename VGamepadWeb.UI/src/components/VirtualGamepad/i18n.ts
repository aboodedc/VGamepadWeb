export type Language = 'ar' | 'en';

export const translations = {
  ar: {
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
    buttonStyle: 'شكل الأزرار (التصميم)',
    xboxDefault: 'Xbox (الافتراضي)',
    controllerTypeLabel: 'نوع اليد (التي تظهر في النظام)',
    enableVibration: 'تفعيل الاهتزاز',
    sensitivity: 'حساسية الأزرار/العصا',
    enableGyro: 'تفعيل الجيروسكوب (حركة الهاتف)',
    motionOrientationLabel: 'اتجاه حركة الهاتف',
    motionOrientationHorizontal: 'أفقي (عرضي - PS4/Xbox)',
    motionOrientationVertical: 'عمودي (طولي - Joy-Con)',
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

    // Motion Controls
    enableMotionControls: 'تفعيل التحكم الحركي (الجيروسكوب)',
    dsuPort: 'منفذ خادم DSU (CemuHook)',

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
    buttonStyle: 'Button Style (Theme)',
    xboxDefault: 'Xbox (Default)',
    controllerTypeLabel: 'Controller Type (shown in system)',
    enableVibration: 'Enable Vibration',
    sensitivity: 'Button / Stick Sensitivity',
    enableGyro: 'Enable Gyroscope (Motion)',
    motionOrientationLabel: 'Motion Orientation',
    motionOrientationHorizontal: 'Horizontal (Landscape - PS4/Xbox)',
    motionOrientationVertical: 'Vertical (Portrait - Joy-Con)',
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

    // Motion Controls
    enableMotionControls: 'Enable Motion Controls (Gyroscope)',
    dsuPort: 'DSU Server Port (CemuHook)',

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
