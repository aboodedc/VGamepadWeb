export type Language = 'ar' | 'en' | 'zh';

const ar = {
    // Connection status
    connected: 'متصل',
    connecting: 'جاري...',
    disconnected: 'غير متصل',

    // TopBar buttons
    hide: 'إخفاء',
    minimize: 'نافذة',
    fullscreen: 'شاشة كاملة',
    done: 'تم',
    edit: 'تعديل',
    touchArea: 'منطقة اللمس',
    buttons: 'الأزرار',
    leftStick: 'عصا اليسار',
    rightStick: 'عصا اليمين',

    // Menu
    reset: 'إعادة ضبط',
    export: 'تصدير',
    import: 'استيراد',
    connection: 'اتصال',
    menu: 'القائمة',
    showBar: 'إظهار الشريط',

    // Settings Modal
    profile: 'البروفايل (تخصيص الأزرار)',
    buttonStyle: 'شكل الأزرار (التصميم)',
    xboxDefault: 'Xbox (الافتراضي)',
    enableVibration: 'تفعيل الاهتزاز',
    on: 'مفعّل',
    off: 'معطّل',
    show: 'إظهار',
    hideControl: 'إخفاء',
    sensitivity: 'حساسية الأزرار/العصا',
    password: 'كلمة المرور (اختياري)',
    passwordPlaceholder: 'اتركها فارغة إذا لم يكن هناك باسورد',
    connect: 'اتصال',

    // Profile prompts/alerts
    defaultProfile: 'التخطيط الافتراضي',
    newProfile: 'بروفايل جديد',
    deleteProfile: 'حذف البروفايل',
    newProfilePrompt: 'أدخل اسم البروفايل الجديد:',
    cannotDeleteOnly: 'لا يمكن حذف البروفايل الوحيد',
    confirmDeleteProfile: 'هل أنت متأكد من حذف هذا البروفايل؟',
    invalidFile: 'ملف غير صالح',
    readFileFailed: 'فشل قراءة الملف',

    // Connection gate (未连接时的全屏覆盖层)
    gateTitle: 'الاتصال بالجهاز',

    // Stable reason codes from the server
    reasonSlotLocked: 'هذا المقعد مقفل، اختر مقعداً آخر',
    reasonSlotTaken: 'هذا المقعد مشغول، اختر مقعداً آخر',
    reasonNoFreeSlot: 'لا توجد مقاعد متاحة',
    reasonSlotInvalid: 'رقم المقعد غير صالح',
    reasonKickedByAdmin: 'تم قطع اتصالك من الخادم',
    connectFailed: 'فشل الاتصال، تحقق من عنوان الخادم',
    authFailed: 'كلمة مرور الخادم غير صحيحة',

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
    resetButton: 'إعادة تعيين الزر',
    closePanel: 'إغلاق',
    editHint: 'اختر أي زر بالضغط عليه لتغيير حجمه وشكله ومكانه بدقة',

    // Language menu
    language: '🌐 اللغة',
    arabic: 'العربية',
    english: 'English',
    chinese: '中文',

    // About
    about: 'حول',
    aboutTitle: 'VGamepad Web',
    aboutTagline: 'يد تحكم افتراضية للكمبيوتر',
    aboutMadeWith: 'صُنع بكل ❤️ من',
    aboutEmail: 'alhatab.edc@gmail.com',
    aboutName: 'Abdullrahman Alhatab',
    aboutGithubStar: 'أعجبك؟ أضف نجمة على GitHub',
    aboutLinkedIn: 'LinkedIn',
    aboutMIT: 'محمي برخصة MIT المفتوحة',
};

const en = {
    // Connection status
    connected: 'Connected',
    connecting: 'Connecting...',
    disconnected: 'Disconnected',

    // TopBar buttons
    hide: 'Hide',
    minimize: 'Window',
    fullscreen: 'Fullscreen',
    done: 'Done',
    edit: 'Edit',
    touchArea: 'Hitboxes',
    buttons: 'Buttons',
    leftStick: 'Left Stick',
    rightStick: 'Right Stick',

    // Menu
    reset: 'Reset',
    export: 'Export',
    import: 'Import',
    connection: 'Connection',
    menu: 'Menu',
    showBar: 'Show Bar',

    // Settings Modal
    profile: 'Profile (Button Layout)',
    buttonStyle: 'Button Style (Theme)',
    xboxDefault: 'Xbox (Default)',
    enableVibration: 'Enable Vibration',
    on: 'On',
    off: 'Off',
    show: 'Show',
    hideControl: 'Hide',
    sensitivity: 'Button / Stick Sensitivity',
    password: 'Password (optional)',
    passwordPlaceholder: 'Leave empty if no password',
    connect: 'Connect',

    // Profile prompts/alerts
    defaultProfile: 'Default Layout',
    newProfile: 'New Profile',
    deleteProfile: 'Delete Profile',
    newProfilePrompt: 'Enter new profile name:',
    cannotDeleteOnly: 'Cannot delete the only profile',
    confirmDeleteProfile: 'Are you sure you want to delete this profile?',
    invalidFile: 'Invalid file',
    readFileFailed: 'Failed to read file',

    // Connection gate (未连接时的全屏覆盖层)
    gateTitle: 'Connect to device',

    // Stable reason codes from the server
    reasonSlotLocked: 'This slot is locked, pick another one',
    reasonSlotTaken: 'This slot is in use, pick another one',
    reasonNoFreeSlot: 'No free slot available',
    reasonSlotInvalid: 'Invalid slot',
    reasonKickedByAdmin: 'The server disconnected you',
    connectFailed: 'Connection failed, check the server URL',
    authFailed: 'Wrong server password',

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
    resetButton: 'Reset Button',
    closePanel: 'Close',
    editHint: 'Tap any button to adjust its size, shape, and position precisely',

    // Language menu
    language: '🌐 Language',
    arabic: 'العربية',
    english: 'English',
    chinese: '中文',

    // About
    about: 'About',
    aboutTitle: 'VGamepad Web',
    aboutTagline: 'Virtual Gamepad for PC',
    aboutMadeWith: 'Made with ❤️ by',
    aboutEmail: 'alhatab.edc@gmail.com',
    aboutName: 'Abdullrahman Alhatab',
    aboutGithubStar: 'Like it? Star on GitHub',
    aboutLinkedIn: 'LinkedIn',
    aboutMIT: 'Licensed under MIT Open Source',
} as const;

export type Translations = typeof en;

// 键集合被 Translations 锁死：少一个键或多一个键都编译不过。
const zh = {
  // Connection status
  connected: '已连接',
  connecting: '连接中…',
  disconnected: '未连接',

  // TopBar buttons
  hide: '隐藏',
  minimize: '窗口',
  fullscreen: '全屏',
  done: '完成',
  edit: '编辑',
  touchArea: '触摸区',
  buttons: '按键',
  leftStick: '左摇杆',
  rightStick: '右摇杆',

  // Menu
  reset: '重置',
  export: '导出',
  import: '导入',
  connection: '连接',
  menu: '菜单',
  showBar: '显示顶栏',

  // Settings Modal
  profile: '配置方案（按键布局）',
  buttonStyle: '按键风格（主题）',
  xboxDefault: 'Xbox（默认）',
  enableVibration: '启动振动',
  on: '开',
  off: '关',
  show: '显示',
  hideControl: '隐藏',
  sensitivity: '按键 / 摇杆灵敏度',
  password: '密码（可选）',
  passwordPlaceholder: '没有密码就留空',
  connect: '连接',

  // Profile prompts/alerts
  defaultProfile: '默认方案',
  newProfile: '新建方案',
  deleteProfile: '删除方案',
  newProfilePrompt: '输入新配置方案的名称：',
  cannotDeleteOnly: '不能删除唯一的配置方案',
  confirmDeleteProfile: '确定要删除这个配置方案吗？',
  invalidFile: '文件无效',
  readFileFailed: '读取文件失败',

  // Connection gate (未连接时的全屏覆盖层)
  gateTitle: '连接设备',

  // Stable reason codes from the server
  reasonSlotLocked: '该槽位已被锁定，请选其他槽位',
  reasonSlotTaken: '该槽位已被占用，请选其他槽位',
  reasonNoFreeSlot: '没有空闲槽位了',
  reasonSlotInvalid: '槽位号无效',
  reasonKickedByAdmin: '你已被管理员踢出',
  connectFailed: '连接失败，请检查服务器地址',
  authFailed: '服务器密码不正确',

  // EditBar
  leftStickFull: '左控制摇杆（LS）',
  rightStickFull: '右控制摇杆（RS）',
  buttonPrefix: '按键',
  size: '大小',
  shape: '形状',
  circular: '● 圆形',
  rectangle: '■ 方形',
  position: '位置',
  align: '对齐',
  resetButton: '重置按键',
  closePanel: '关闭',
  editHint: '点选任意按键，可精确调整它的大小、形状和位置',

  // Language menu
  language: '🌐 语言',
  arabic: 'العربية',
  english: 'English',
  chinese: '中文',

  // About
  about: '关于',
  aboutTitle: 'VGamepad Web',
  aboutTagline: 'PC 虚拟手柄',
  aboutMadeWith: '用心制作 ❤️',
  aboutEmail: 'alhatab.edc@gmail.com',
  aboutName: 'Abdullrahman Alhatab',
  aboutGithubStar: '觉得好用？去 GitHub 点个星',
  aboutLinkedIn: 'LinkedIn',
  aboutMIT: '基于 MIT 开源协议',
} satisfies Record<keyof Translations, string>;

export const translations = { ar, en, zh } as const;

export type TranslationSet = typeof translations[Language];
