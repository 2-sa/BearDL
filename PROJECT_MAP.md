# خريطة مسار الاسم والترجمة

هذه خريطة محدودة للمسار الذي روجع، وليست بديلًا عن دليل المشروع.
الحالة والإذن الساري في [docs/CURRENT_TASK.md](docs/CURRENT_TASK.md).

## [TECH_STACK]

- .NET 10، وNickvision.Desktop 2026.5.3، وgettext؛ واجهتا WinUI وGTK.
- `Nickvision.Common.targets` يجمع ملفات اللغات باسم `parabolic.mo`.

## [SYSTEM_FLOW]

- `L10N-01`: بدء التطبيق → تهيئة AppInfo → TranslationService يشتق المجال
  من EnglishShortName → اكتشاف مجلدات اللغات → تحميل العربية من `ar/parabolic.mo`.
- النتيجة المطلوبة: ظهور العربية في قائمة اللغات وقراءة ترجماتها، مع بقاء
  اسم العرض BearDL. اختلاف مجال التهيئة عن اسم ملف MO يجعل اللغات غير مكتشفة.
- `THEME-01`: بدء التطبيق أو تغيير السمة في الإعدادات → `MainWindow.ApplyTheme()`
  يضبط `PreferredTheme` لشريط العنوان و`RequestedTheme` على `WindowRoot` (`Window.Content`)
  و`MainGrid` معًا → تحدّث `MicaBackdrop` و`PopupRoot` خلفية النافذة والقوائم المنبثقة
  وفق السمة المختارة بدل البقاء على سمة نظام Windows.
- `THEME-02`: اختيار سمة `Glass` («زجاجي») في الإعدادات → استدعاء `GlassThemeHelper.Apply`
  في WinUI (`ClearGlassBackdrop` بشفافية DWM حقيقية + حواف زجاجية عاكسة + بطاقات عائمة)
  وفي GTK (`glass-mode` CSS مع `glass-theme.css`).

## [ARCHITECTURE]

- `HostApplicationBuilderExtensions.cs` في Shared يهيئ الهوية الداخلية الثابتة.
- `MainWindowController.cs` يضبط ShortName الظاهر إلى BearDL؛ لا يغيّر EnglishShortName.
- `Nickvision.Common.targets` يولد الكتالوجات للواجهتين.
- `tests/windows/Verify-TranslationCatalogs.ps1` يطابق المجال مع كل ملفات اللغات
  المنشورة ويقرأ وجود ترجمة عربية؛ يعمل بعد Publish وقبل إنشاء المثبّت على Actions.
- `Nickvision.Parabolic.WinUI/Views/MainWindow.xaml` و`MainWindow.xaml.cs`: الجذر الخارجي
  `WindowRoot` يحتفظ باتجاه LTR لمناطق النقر في شريط العنوان ويتلقى `RequestedTheme`
  مباشرةً لأن `MicaBackdrop` و`PopupRoot` يراقبان جذر `Window.Content` لا الشبكة الداخلية `MainGrid`.
- عزل الوضع الزجاجي (`Theme.Glass`):
  - `Nickvision.Parabolic.WinUI/Helpers/GlassThemeHelper.cs`: يحوي كامل منطق وألوان وطبقات الوضع الزجاجي لـWinUI 3.
  - `Nickvision.Parabolic.GNOME/Helpers/GlassThemeHelper.cs` و`Resources/glass-theme.css`: يحويان منطق وتنسيقات الوضع الزجاجي لـGTK4/libadwaita.
- مجلدات البيانات ومعرفات التطبيق والبروتوكول منفصلة عن نصوص العرض.

## [ORPHANS & PENDING]

- `L10N-01`: نُفّذ فحص الحزمة `Verify-TranslationCatalogs.ps1` ونجح على Actions في البناء
  `38990949083` (`2026.5.124` من الكوميت `c17c224d`). أكدت لقطة المستخدم في 2026-10-18
  عودة الواجهة العربية واتجاه RTL وظهور الاسم `BearDL`.
- `THEME-01` و`THEME-02`: أُصلح الوضع الفاتح وأُضيف الوضع الزجاجي (`Theme.Glass`) محليًا
  في ملفات معزولة للواجهتين. التجميع والتحقق البصري على Windows وmacOS/Linux معلّقان لحين الإذن بالكوميت والبناء على GitHub Actions.
