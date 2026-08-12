<template>
  <div class="h-dvh w-dvw">
    <NuxtRouteAnnouncer />
    <!-- Static page-key: pages/id/[[id]].vue's optional param otherwise makes
         Nuxt remount the whole page (and its DOM - list scroll, filters)
         every time router.replace toggles between /id and /id/<id>. -->
    <NuxtPage :page-key="'app'" />
  </div>
</template>
