# Proqramın Hazırlanması Üçün Qlobal İş Siyahısı

Bu siyahı yalnız proqramın hazırlanacaq hissələrini əhatə edir: istifadəçinin gördüyü pəncərələr və ekranlar, arxa fonda işləyən host xidmətləri, plugin/licensing runtime hissələri, admin alətləri və installer artefaktları.

Test yazılması, evidence faylları, sprint checklist-ləri, DoD qeydləri, governance testləri və oxşar proses işləri bu siyahıya ayrıca iş kimi daxil edilməyib.

## Təməl Platforma və Host Runtime

1. Core nəticə modeli, səhv kodları, correlation id və versiya məlumatları üçün ortaq primitives hazırlanmalıdır.
2. Plugin-lərin istifadə edəcəyi public contracts və SDK səthi hazırlanmalıdır.
3. Plugin-visible storage contract-ları və saxlanma scope-ları hazırlanmalıdır.
4. Policy, operation, authorization, navigation, event, page, background task və licensing contract-ları hazırlanmalıdır.
5. Host configuration modeli hazırlanmalıdır: environment, runtime folders, security floors və source precedence qaydaları.
6. Host runtime qovluqları LocalAppData altında ayrılmalıdır: data, logs, plugins, staging, diagnostics və temporary sahələr.
7. Local diagnostics sistemi hazırlanmalıdır: structured log, redaction, retention və export paketi.
8. Append-only audit boundary hazırlanmalıdır: audit event taxonomy, severity və retention qaydaları.
9. Protected local persistence arxitekturası hazırlanmalıdır.
10. Encrypted local database provider hazırlanmalıdır.
11. Database WAL, journal, temp və backup davranışları qorunmuş persistence modelinə uyğunlaşdırılmalıdır.
12. Pre-login mərhələsində işləyəcək qapalı table set hazırlanmalıdır.
13. Post-login migration framework hazırlanmalıdır.
14. Protected material xidməti hazırlanmalıdır: DPAPI, app entropy, stable device id və secret protection.
15. Transaction və crash-recovery boundary hazırlanmalıdır.

## Login, Owner Bootstrap və Session

16. Tətbiq login-first açılmalıdır: login tamamlanmadan shell, plugin discovery, activation, import/export və background işlər başlamamalıdır.
17. Login pəncərəsindən əvvəl single-instance coordinator işləməlidir.
18. Username canonicalization və username validation hazırlanmalıdır.
19. Display name validation hazırlanmalıdır.
20. Password policy validation hazırlanmalıdır.
21. Credential store hazırlanmalıdır.
22. KDF policy resolver və password hashing floor-ları hazırlanmalıdır.
23. Login service hazırlanmalıdır.
24. Login throttle xidməti hazırlanmalıdır: uğursuz cəhdlərdə gecikmə və countdown davranışı.
25. İlk Owner bootstrap flow hazırlanmalıdır.
26. Login pəncərəsi hazırlanmalıdır: username, password, ilk Owner üçün display name, remember-me, login button və error/throttle state-ləri.
27. Login ViewModel hazırlanmalıdır.
28. Remember-me slot xidməti hazırlanmalıdır.
29. Session record və session store hazırlanmalıdır.
30. Session lifecycle service hazırlanmalıdır: login, active session, lock, logout, idle timeout, absolute timeout və deactivation.
31. Active session accessor hazırlanmalıdır.
32. Lock pəncərəsi və Lock ViewModel hazırlanmalıdır.
33. Password change flow hazırlanmalıdır.
34. Owner recovery request export flow hazırlanmalıdır: `.dobbyrecoveryrequest` faylı yaradılmalıdır.
35. Owner recovery response import flow hazırlanmalıdır: `.dobbyrecovery` faylı yoxlanmalı və istifadə olunmalıdır.
36. Recovery password reset dialog hazırlanmalıdır.
37. Recovery payload canonical JSON, signature, expiry, nonce, one-use və device-bound yoxlamaları hazırlanmalıdır.
38. Hardware identity provider və device fingerprint composer hazırlanmalıdır.
39. Pre-login instrumentation hazırlanmalıdır.
40. Login, lock və recovery ekranları üçün lokalizasiya açarları hazırlanmalıdır.

## Authorization, İstifadəçilər və Settings

41. Operation target registry hazırlanmalıdır.
42. Built-in operation target-lar və operation verbs hazırlanmalıdır.
43. Grant-only, default-deny permission modeli hazırlanmalıdır.
44. Permission grant store hazırlanmalıdır.
45. Operation authorization policy hazırlanmalıdır.
46. User management authorization guard hazırlanmalıdır.
47. Owner və self-grant guard-ları hazırlanmalıdır.
48. Managed user service hazırlanmalıdır: create, update, deactivate, reactivate, reset password, grant və revoke əməliyyatları.
49. User Management ekranı hazırlanmalıdır.
50. User management audit coordinator hazırlanmalıdır.
51. Settings navigation tree hazırlanmalıdır.
52. Appearance settings hazırlanmalıdır.
53. Account settings hazırlanmalıdır.
54. Security/Session settings hazırlanmalıdır.
55. About info provider və About ekranı hazırlanmalıdır.
56. Settings preference source və typed settings preference səthi hazırlanmalıdır.
57. Settings, About və User Management üçün lokalizasiya açarları hazırlanmalıdır.

## Authenticated Shell və Görünən UI

58. Login-dən sonra açılan authenticated shell hazırlanmalıdır.
59. MainWindow layout hazırlanmalıdır: Homebar/TopToolbar, Sidebar, Third Bar, Main Content, bottom status/progress, notification/feedback sahəsi və minimized work strip.
60. Profile/session menu hazırlanmalıdır: lock, logout, password, session state və əlaqəli actions.
61. Built-in Home route hazırlanmalıdır.
62. Built-in Plugins route hazırlanmalıdır.
63. Built-in Settings route hazırlanmalıdır.
64. Route registry və fallback route davranışı hazırlanmalıdır.
65. Navigation composer hazırlanmalıdır: built-in route-lar, plugin contribution-lar, hierarchy və ordering.
66. ShellRouteHost hazırlanmalıdır.
67. ShellViewModel hazırlanmalıdır.
68. Sidebar state hazırlanmalıdır: collapse, selected node, open nodes və user-scoped preference.
69. Third Bar state hazırlanmalıdır: route/context əsaslı görünmə, ordering və preference.
70. Homebar/TopToolbar state hazırlanmalıdır: theme switch, plugin action ordering və hidden state preference.
71. Main Content host hazırlanmalıdır: built-in page-lər və plugin page-lər burada göstərilməlidir.
72. Bottom status və progress surface hazırlanmalıdır.
73. Notification və feedback state-ləri hazırlanmalıdır.
74. Minimized work-session strip hazırlanmalıdır.
75. Shell view preference-ləri saxlanmalıdır: window size/position, sidebar state, toolbar order və last active module.
76. Theme tokens və design tokens hazırlanmalıdır.
77. Light theme resource dictionary hazırlanmalıdır.
78. Dark theme resource dictionary hazırlanmalıdır.
79. High contrast və reduced motion davranışları hazırlanmalıdır.
80. Appearance Settings ilə Homebar/TopToolbar theme switch eyni preference üzərindən sinxron işləməlidir.
81. Lokalizasiya sistemi hazırlanmalıdır: `az-AZ` əsas dil, optional `en-US` və `tr-TR` skeleton-ları, missing-key diagnostics.
82. Work session modeli hazırlanmalıdır: process-lifetime session, minimize, restore, warning choices, close və cancel.
83. Lock, logout və plugin lifecycle zamanı work session davranışı hazırlanmalıdır.
84. Authenticated shell daxilindən diagnostics export action hazırlanmalıdır.
85. WPF UI dispatcher adapter hazırlanmalıdır.

## Plugin Runtime və Plugin Host

86. `.dobbyplugin` package reader hazırlanmalıdır.
87. Plugin manifest validation hazırlanmalıdır.
88. Plugin package fingerprint və file hash yoxlamaları hazırlanmalıdır.
89. Plugin package trust verification hazırlanmalıdır: signature, trust anchor və package fingerprint.
90. Plugin installer hazırlanmalıdır: staging, install state və metadata cache.
91. Installed plugin state store hazırlanmalıdır.
92. Plugin compatibility və dependency graph hazırlanmalıdır.
93. Deferred post-login plugin activation hazırlanmalıdır.
94. Per-plugin AssemblyLoadContext hazırlanmalıdır.
95. Plugin DI registration bridge hazırlanmalıdır.
96. Plugin service collection builder hazırlanmalıdır.
97. Plugin extension point registry hazırlanmalıdır.
98. Plugin lifetime manager hazırlanmalıdır.
99. Plugin operation target contribution builder hazırlanmalıdır.
100. Plugin operation target contribution validator hazırlanmalıdır.
101. Plugin operation target-ları authorization registry-yə activation-dan əvvəl seed edilməlidir.
102. Plugin navigation contribution mapper hazırlanmalıdır.
103. Plugin page render registry hazırlanmalıdır.
104. Plugin page-lərin Main Content daxilində göstərilməsi hazırlanmalıdır.
105. Plugin background operation snapshot-ları shell status/progress səthinə ötürülməlidir.
106. Plugin exception boundary hazırlanmalıdır.
107. Plugin fault state-ləri user-facing və non-disclosing formada göstərilməlidir.
108. Production və Dev/QA trust profile-ları ayrılmalıdır.

## Licensing və Runtime Mode Enforcement

109. `.dobbylicense` artifact parser hazırlanmalıdır.
110. License canonical JSON writer hazırlanmalıdır.
111. License signature verifier hazırlanmalıdır.
112. License payload validator hazırlanmalıdır.
113. Customer/deployment context hazırlanmalıdır.
114. License device fingerprint hazırlanmalıdır.
115. License revision policy hazırlanmalıdır.
116. License import service hazırlanmalıdır.
117. License state store hazırlanmalıdır.
118. `Plugins > License` import ekranı və coordinator hazırlanmalıdır.
119. License file picker hazırlanmalıdır.
120. Runtime mode service hazırlanmalıdır: FullAccess, ReadOnlyExpired və Denied.
121. Plugin-lərə yalnız `ICapabilityLicensing.GetCurrentMode()` səthi verilməlidir.
122. Host plugin activation zamanı license mode yenidən yoxlanmalıdır.
123. ReadOnlyExpired mode üçün mutation əməliyyatları bloklanmalıdır.
124. ReadOnlyExpired mode üçün plugin storage write əməliyyatları bloklanmalıdır.
125. ReadOnlyExpired mode üçün background task start əməliyyatları bloklanmalıdır.
126. ReadOnlyExpired mode üçün `.dobbyplugin` import/install bloklanmalıdır.
127. ReadOnlyExpired mode üçün domain import/export, report və file output əməliyyatları bloklanmalıdır.
128. ReadOnlyExpired mode üçün print-to-file, PDF, virtual və unknown print target-ları bloklanmalıdır.
129. ReadOnlyExpired mode-da fiziki printerə çap yalnız physical target təsdiqlənəndə icazəli olmalıdır.
130. ReadOnlyExpired mode-da `.dobbylicense` import icazəli qalmalıdır.
131. Windows print target classifier adapter hazırlanmalıdır.
132. License status və rejection mesajları shell/plugin manager UI-da göstərilməlidir.

## Plugin Manager, Sample Plugin və Admin Alətləri

133. Plugins Manager ekranı hazırlanmalıdır.
134. Plugins Manager list view hazırlanmalıdır: plugin adı, status, version, license/trust state və fault state.
135. Plugins Manager detail view hazırlanmalıdır.
136. Plugin install action hazırlanmalıdır.
137. Plugin enable action hazırlanmalıdır.
138. Plugin disable action hazırlanmalıdır.
139. Plugin uninstall action hazırlanmalıdır.
140. Plugin lifecycle audit hazırlanmalıdır.
141. Plugin lifecycle state və pending deletion/restart davranışı hazırlanmalıdır.
142. Plugin management permission target hazırlanmalıdır.
143. User-facing plugin rejection mesajları hazırlanmalıdır.
144. Sample plugin hazırlanmalıdır: manifest, icon, page, operation target və extension registration.
145. Sample plugin Main Content daxilində işləməlidir.
146. Sample plugin operation denial, Result boundary, fault, progress və work-session davranışlarını göstərməlidir.
147. Admin command host hazırlanmalıdır.
148. `sign-plugin-package` admin aləti hazırlanmalıdır.
149. `create-plugin-license` admin aləti hazırlanmalıdır.
150. `verify-plugin-package` admin aləti hazırlanmalıdır.
151. `create-recovery-response` admin aləti hazırlanmalıdır.
152. Production build-lərdə Dev/QA artifact-lərin rədd edilməsi hazırlanmalıdır.

## Installer və İstifadəçiyə Verilən Artefakt

153. MSIX installer əsas yayım formatı kimi hazırlanmalıdır.
154. Installer upgrade davranışı hazırlanmalıdır.
155. Upgrade zamanı app data qorunmalıdır.
156. Portable build hazırlanmalıdır.
157. Portable build üçün dev-only və support constraint-ləri aydın enforced edilməlidir.
158. Installer artefaktı mövcud app data ilə işə düşməli və tətbiqi login-first startup davranışı ilə açmalıdır.

## Bu Siyahıya Daxil Edilməyənlər

- Test class-ları, test case-ləri və test infrastructure işləri.
- Evidence faylları və sprint completion qeydləri.
- Governance checklist-ləri və DoD sənədləşdirməsi.
- Məhsulun P1 scope-dan kənarda saxlanmış hissələr: full Admin UI, marketplace, plugin update/repair/downgrade, PostgreSQL integration, runtime language switching və persistent work sessions.
