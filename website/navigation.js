document.addEventListener('DOMContentLoaded', () => {
  const header = document.querySelector('[data-site-navigation]');
  if (!header) return;

  const menuButton = header.querySelector('.site-menu-button');
  const container = header.querySelector('.site-nav-groups');
  const groups = [...header.querySelectorAll('.site-nav-group')];
  const narrow = matchMedia('(max-width: 48rem)');
  const setOpen = (group, open) => {
    group.toggleAttribute('data-open', open);
    group.querySelector('.site-nav-heading').setAttribute('aria-expanded', String(open));
  };
  const closeGroups = () => groups.forEach(group => setOpen(group, false));
  const closeMenu = () => {
    closeGroups();
    menuButton.setAttribute('aria-expanded', 'false');
    container.removeAttribute('data-menu-open');
  };

  menuButton.addEventListener('click', () => {
    const open = menuButton.getAttribute('aria-expanded') !== 'true';
    closeGroups();
    menuButton.setAttribute('aria-expanded', String(open));
    container.toggleAttribute('data-menu-open', open);
  });

  groups.forEach(group => {
    const button = group.querySelector('.site-nav-heading');
    const links = [...group.querySelectorAll('.site-nav-menu a')];
    button.addEventListener('click', () => {
      const open = !group.hasAttribute('data-open');
      closeGroups();
      setOpen(group, open);
    });
    button.addEventListener('keydown', event => {
      if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;
      event.preventDefault();
      closeGroups();
      setOpen(group, true);
      (event.key === 'ArrowDown' ? links[0] : links.at(-1))?.focus();
    });
    links.forEach((link, index) => link.addEventListener('keydown', event => {
      let destination;
      if (event.key === 'ArrowDown') destination = links[(index + 1) % links.length];
      if (event.key === 'ArrowUp') destination = links[(index + links.length - 1) % links.length];
      if (event.key === 'Home') destination = links[0];
      if (event.key === 'End') destination = links.at(-1);
      if (!destination) return;
      event.preventDefault();
      destination.focus();
    }));
  });

  // A disclosure is always a button. Links always navigate on their first activation.
  header.addEventListener('keydown', event => {
    if (event.key !== 'Escape') return;
    const group = event.target.closest('.site-nav-group[data-open]');
    if (group) {
      event.preventDefault();
      setOpen(group, false);
      group.querySelector('.site-nav-heading').focus();
    } else if (menuButton.getAttribute('aria-expanded') === 'true') {
      event.preventDefault();
      closeMenu();
      menuButton.focus();
    }
  });
  header.addEventListener('focusout', event => {
    if (!header.contains(event.relatedTarget)) closeMenu();
    else groups.forEach(group => {
      if (!group.contains(event.relatedTarget)) setOpen(group, false);
    });
  });
  document.addEventListener('click', event => {
    if (!header.contains(event.target)) closeMenu();
    else if (event.target.closest('a')) closeMenu();
  });
  narrow.addEventListener('change', closeMenu);

  const updateCurrent = () => {
    header.querySelectorAll('a').forEach(link => {
      const url = new URL(link.href, location.href);
      const samePage = url.origin === location.origin && url.pathname === location.pathname;
      const active = samePage && (url.hash ? url.hash === location.hash : !location.hash || url.pathname !== '/token-economy/');
      if (active) link.setAttribute('aria-current', url.hash ? 'location' : 'page');
      else link.removeAttribute('aria-current');
    });
  };
  const updateOffset = () => document.documentElement.style.setProperty(
    '--site-header-offset', `${header.getBoundingClientRect().height + 16}px`);
  new ResizeObserver(updateOffset).observe(header);

  // Keep a deep link aligned while evidence loads above it. User scrolling wins.
  let followAnchor = Boolean(location.hash);
  let scheduled = false;
  const alignAnchor = () => {
    if (!followAnchor || scheduled) return;
    scheduled = true;
    requestAnimationFrame(() => {
      scheduled = false;
      if (!followAnchor || !location.hash) return;
      let id;
      try { id = decodeURIComponent(location.hash.slice(1)); } catch { return; }
      document.getElementById(id)?.scrollIntoView({ behavior: 'instant', block: 'start' });
    });
  };
  const stopFollowing = () => { followAnchor = false; };
  window.addEventListener('wheel', stopFollowing, { passive: true });
  window.addEventListener('touchmove', stopFollowing, { passive: true });
  window.addEventListener('pointerdown', event => {
    if (!event.target.closest('a, button')) stopFollowing();
  }, { passive: true });
  window.addEventListener('keydown', event => {
    if (['ArrowDown', 'ArrowUp', 'PageDown', 'PageUp', 'Home', 'End', ' '].includes(event.key)
        && !event.defaultPrevented
        && !event.target.closest('button, input, textarea, select, [contenteditable]')) stopFollowing();
  });
  window.addEventListener('hashchange', () => {
    followAnchor = true;
    updateCurrent();
    alignAnchor();
  });
  document.addEventListener('click', event => {
    const link = event.target.closest('a[href]');
    if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    const url = new URL(link.href, location.href);
    if (url.origin !== location.origin || url.pathname !== location.pathname || !url.hash) return;
    followAnchor = true;
    requestAnimationFrame(() => {
      updateOffset();
      alignAnchor();
      let target;
      try { target = document.getElementById(decodeURIComponent(url.hash.slice(1))); } catch { return; }
      if (target) {
        if (!target.hasAttribute('tabindex')) target.setAttribute('tabindex', '-1');
        target.focus({ preventScroll: true });
      }
    });
  });
  const contentObserver = new ResizeObserver(alignAnchor);
  const main = document.querySelector('main');
  if (main && document.querySelector('[data-pending-content]')) contentObserver.observe(main);
  document.addEventListener('site:content-ready', () => {
    alignAnchor();
    contentObserver.disconnect();
  }, { once: true });
  // The guide contents follows its own section, independently of the site menu.
  const contents = document.querySelector('.docs-toc');
  if (contents) {
    const entries = [...contents.querySelectorAll('a[href]')].flatMap(link => {
      const url = new URL(link.href, location.href);
      if (url.origin !== location.origin || url.pathname !== location.pathname || !url.hash) return [];
      let target;
      try { target = document.getElementById(decodeURIComponent(url.hash.slice(1))); } catch { return []; }
      return target ? [{ link, target, hash: url.hash }] : [];
    });
    let current;
    let contentsScheduled = false;
    const markCurrent = entry => {
      if (entry !== current) {
        current = entry;
        entries.forEach(candidate => {
          if (candidate === entry) candidate.link.setAttribute('aria-current', 'location');
          else candidate.link.removeAttribute('aria-current');
        });
      }
      // Reveal a late chapter inside the sticky menu without scrolling the page.
      if (entry && contents.scrollHeight > contents.clientHeight) {
        const menuRect = contents.getBoundingClientRect();
        const linkRect = entry.link.getBoundingClientRect();
        if (linkRect.top < menuRect.top + 8) contents.scrollTop += linkRect.top - menuRect.top - 8;
        else if (linkRect.bottom > menuRect.bottom - 8) contents.scrollTop += linkRect.bottom - menuRect.bottom + 8;
      }
    };
    const updateContents = () => {
      contentsScheduled = false;
      // A short chapter near the page end cannot always reach the sticky header.
      // Keep a requested anchor selected until the reader starts scrolling.
      const requested = entries.find(entry => entry.hash === location.hash);
      if (followAnchor && requested) return markCurrent(requested);
      const offset = parseFloat(getComputedStyle(document.documentElement)
        .getPropertyValue('--site-header-offset')) || 112;
      let visible = entries[0];
      for (const entry of entries) {
        if (entry.target.getBoundingClientRect().top > offset + 1) break;
        visible = entry;
      }
      const pageHeight = document.documentElement.scrollHeight;
      if (pageHeight > innerHeight && scrollY + innerHeight >= pageHeight - 1) visible = entries.at(-1);
      markCurrent(visible);
    };
    const scheduleContents = () => {
      if (contentsScheduled) return;
      contentsScheduled = true;
      requestAnimationFrame(updateContents);
    };
    window.addEventListener('scroll', scheduleContents, { passive: true });
    window.addEventListener('resize', scheduleContents);
    window.addEventListener('hashchange', scheduleContents);
    document.addEventListener('click', event => {
      const link = event.target.closest('a[href]');
      if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
      const url = new URL(link.href, location.href);
      if (url.origin === location.origin && url.pathname === location.pathname && url.hash) scheduleContents();
    });
    const contentsObserver = new ResizeObserver(scheduleContents);
    contentsObserver.observe(header);
    const body = document.querySelector('.docs-body');
    if (body) contentsObserver.observe(body);
    scheduleContents();
  }
  document.documentElement.classList.add('site-nav-enhanced');
  updateOffset();
  updateCurrent();
  alignAnchor();
});
