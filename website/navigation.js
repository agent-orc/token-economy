document.documentElement.classList.add('site-nav-enhanced');

document.addEventListener('DOMContentLoaded', () => {
  const header = document.querySelector('[data-site-navigation]');
  if (!header) return;

  const menuButton = header.querySelector('.site-menu-button');
  const groupsContainer = header.querySelector('.site-nav-groups');
  const groups = [...header.querySelectorAll('.site-nav-group')];
  const headings = groups.map(group => group.querySelector('.site-nav-heading'));

  const setGroupOpen = (group, open) => {
    if (open) group.setAttribute('data-open', 'true');
    else group.removeAttribute('data-open');
    group.querySelector('.site-nav-heading').setAttribute('aria-expanded', String(open));
  };

  const closeGroups = except => groups.forEach(group => {
    if (group !== except) setGroupOpen(group, false);
  });

  const openGroup = (group, focusAt) => {
    closeGroups(group);
    setGroupOpen(group, true);
    const links = [...group.querySelectorAll('.site-nav-menu a')];
    if (focusAt === 'first') links[0]?.focus();
    if (focusAt === 'last') links.at(-1)?.focus();
  };

  menuButton.addEventListener('click', () => {
    const open = menuButton.getAttribute('aria-expanded') !== 'true';
    menuButton.setAttribute('aria-expanded', String(open));
    if (open) groupsContainer.setAttribute('data-menu-open', 'true');
    else groupsContainer.removeAttribute('data-menu-open');
    if (!open) closeGroups();
  });

  groups.forEach((group, groupIndex) => {
    const heading = headings[groupIndex];
    const links = [...group.querySelectorAll('.site-nav-menu a')];

    heading.addEventListener('click', event => {
      if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const narrow = matchMedia('(max-width: 40rem)').matches;
      if (narrow) {
        event.preventDefault();
        const open = !group.hasAttribute('data-open');
        closeGroups(open ? group : undefined);
        setGroupOpen(group, open);
      } else if (!group.hasAttribute('data-open')) {
        event.preventDefault();
        openGroup(group);
      }
    });

    heading.addEventListener('keydown', event => {
      if (event.key === ' ') {
        event.preventDefault();
        const open = !group.hasAttribute('data-open');
        closeGroups(open ? group : undefined);
        setGroupOpen(group, open);
      } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        openGroup(group, event.key === 'ArrowDown' ? 'first' : 'last');
      } else if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
        event.preventDefault();
        const step = event.key === 'ArrowRight' ? 1 : -1;
        headings[(groupIndex + step + headings.length) % headings.length].focus();
      } else if (event.key === 'Escape') {
        setGroupOpen(group, false);
      }
    });

    links.forEach((link, linkIndex) => link.addEventListener('keydown', event => {
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        const step = event.key === 'ArrowDown' ? 1 : -1;
        links[(linkIndex + step + links.length) % links.length].focus();
      } else if (event.key === 'Home' || event.key === 'End') {
        event.preventDefault();
        links[event.key === 'Home' ? 0 : links.length - 1].focus();
      } else if (event.key === 'Escape') {
        event.preventDefault();
        setGroupOpen(group, false);
        heading.focus();
      }
    }));
  });

  header.addEventListener('mouseover', event => {
    const group = event.target.closest('.site-nav-group');
    if (group && matchMedia('(min-width: 40.001rem)').matches) {
      closeGroups(group);
      group.querySelector('.site-nav-heading').setAttribute('aria-expanded', 'true');
    }
  });

  header.addEventListener('mouseleave', () => closeGroups());

  document.addEventListener('click', event => {
    if (!header.contains(event.target)) {
      closeGroups();
      menuButton.setAttribute('aria-expanded', 'false');
      groupsContainer.removeAttribute('data-menu-open');
    }
  });

  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && menuButton.getAttribute('aria-expanded') === 'true') {
      closeGroups();
      menuButton.setAttribute('aria-expanded', 'false');
      groupsContainer.removeAttribute('data-menu-open');
      menuButton.focus();
    }
  });
});
