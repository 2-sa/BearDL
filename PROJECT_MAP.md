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

## [ARCHITECTURE]

- `HostApplicationBuilderExtensions.cs` في Shared يهيئ الهوية الداخلية الثابتة.
- `MainWindowController.cs` يضبط ShortName الظاهر إلى BearDL؛ لا يغيّر EnglishShortName.
- `Nickvision.Common.targets` يولد الكتالوجات للواجهتين.
- `tests/windows/Verify-TranslationCatalogs.ps1` يطابق المجال مع كل ملفات اللغات
  المنشورة ويقرأ وجود ترجمة عربية؛ يعمل بعد Publish وقبل إنشاء المثبّت على Actions.
- مجلدات البيانات ومعرفات التطبيق والبروتوكول منفصلة عن نصوص العرض.

## [ORPHANS & PENDING]

- `L10N-01`: أُصلح المصدر محليًا. فحص الحزمة الجديد لم يُشغّل بعد على Actions؛
  يتطلب تسليم التعديل بإذن المستخدم. بناء التسمية السابق نجح لكنه لم يكشف هذا الخلل.
- التحقق البصري من قائمة العربية والواجهة RTL واسم BearDL ما زال مطلوبًا بعد
  بناء النسخة المصححة وتثبيتها. نجاح فحص الكتالوجات لا يثبت المظهر أثناء التشغيل.
